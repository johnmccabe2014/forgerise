using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Features.Video.Dtos;
using ForgeRise.Api.Features.Video.Options;
using ForgeRise.Api.Features.Video.Services;
using ForgeRise.Api.WelfareModule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace ForgeRise.Api.Features.Video.Endpoints;

/// <summary>
/// Disables MVC's default form value provider so that the request body
/// is not consumed by model binding before the action streams it through
/// <see cref="MultipartReader"/>. This is the canonical ASP.NET Core
/// pattern for streamed multipart uploads.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var factories = context.ValueProviderFactories;
        for (var i = factories.Count - 1; i >= 0; i--)
        {
            var t = factories[i].GetType().Name;
            if (t is "FormValueProviderFactory" or "FormFileValueProviderFactory" or "JQueryFormValueProviderFactory")
            {
                factories.RemoveAt(i);
            }
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}

/// <summary>
/// V2 surface: single-shot upload + status poll. The endpoints 404 when the
/// module is disabled, so the surface stays invisible to clients in
/// every environment until a deploy explicitly flips
/// <c>Features:Video:Enabled</c>.
/// </summary>
[ApiController]
[Authorize]
[Route("v1/teams/{teamId:guid}/videos")]
public sealed class UploadsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IUploadService _uploads;
    private readonly VideoFeatureOptions _flag;
    private readonly VideoStorageOptions _storage;

    public UploadsController(
        AppDbContext db,
        IUploadService uploads,
        IOptions<VideoFeatureOptions> flag,
        IOptions<VideoStorageOptions> storage)
    {
        _db = db;
        _uploads = uploads;
        _flag = flag.Value;
        _storage = storage.Value;
    }

    /// <summary>
    /// Single-shot upload. Multipart body with one file field. The MIME
    /// allow-list is enforced by sniff inside <see cref="UploadService"/>;
    /// the controller only handles HTTP shaping + rate limit + flag gate.
    /// </summary>
    [HttpPost("uploads")]
    [EnableRateLimiting("video-upload")]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> Upload(Guid teamId, CancellationToken ct)
    {
        if (!_flag.Enabled) return NotFound();

        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;
        var userId = User.TryGetUserId()!.Value;

        if (!IsMultipart(Request.ContentType, out var boundary))
        {
            return Problem(statusCode: 415, type: "multipart_required",
                detail: "Use multipart/form-data with a single 'file' part.");
        }

        // Hard request-level cap before reading: Content-Length + 1 MiB headroom.
        if (Request.ContentLength is { } cl && cl > _storage.MaxUploadBytes + (1L << 20))
        {
            return Problem(statusCode: 413, type: "payload_too_large");
        }

        // 5-min request timeout (security review iter1, finding F2).
        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        requestCts.CancelAfter(TimeSpan.FromMinutes(5));

        // Defensive: ensure the body has not been consumed by another
        // component before we hand it to MultipartReader. The TestHost in
        // some configurations exposes a non-replayable stream.
        if (!Request.Body.CanRead)
        {
            return Problem(statusCode: 500, type: "body_unreadable");
        }

        var reader = new MultipartReader(boundary, Request.Body);
        MultipartSection? section;
        try
        {
            while ((section = await reader.ReadNextSectionAsync(requestCts.Token)) is not null)
            {
                var disp = ContentDispositionHeaderValue.Parse(section.ContentDisposition);
                if (!disp.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!disp.FileName.HasValue && !disp.FileNameStar.HasValue)
                    continue;

                var fileName = (disp.FileNameStar.HasValue ? disp.FileNameStar.Value : disp.FileName.Value!)
                    .Trim('"');
                fileName = SanitiseFileName(fileName);

                var outcome = await _uploads.UploadAsync(
                    teamId, userId, fileName, section.Body, requestCts.Token);

                return outcome switch
                {
                    { Asset: { } a } => Created(
                        $"/v1/teams/{teamId:N}/videos/{a.Id:N}",
                        new UploadResponse(
                            a.Id,
                            a.OriginalFileName,
                            a.SizeBytes,
                            a.ContentSha256 ?? string.Empty,
                            a.ProcessingState.ToString(),
                            a.CreatedAt)),
                    { Failure: UploadFailure.UnsupportedMediaType } =>
                        Problem(statusCode: 415, type: "unsupported_media_type"),
                    { Failure: UploadFailure.PayloadTooLarge } =>
                        Problem(statusCode: 413, type: "payload_too_large"),
                    { Failure: UploadFailure.TeamQuotaExceeded } =>
                        Problem(statusCode: 413, type: "team_quota_exceeded"),
                    { Failure: UploadFailure.StorageUnavailable } =>
                        Problem(statusCode: 503, type: "storage_unavailable"),
                    _ => StatusCode(500),
                };
            }
        }
        catch (OperationCanceledException) when (requestCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return Problem(statusCode: 408, type: "upload_timeout");
        }

        return Problem(statusCode: 400, type: "no_file_part");
    }

    /// <summary>Cheap polling: project state only, no joins.</summary>
    [HttpGet("{videoId:guid}/status")]
    public async Task<IActionResult> Status(Guid teamId, Guid videoId, CancellationToken ct)
    {
        if (!_flag.Enabled) return NotFound();
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var status = await _db.VideoAssets
            .Where(a => a.TeamId == teamId && a.Id == videoId && a.DeletedAt == null)
            .Select(a => new VideoStatusResponse(a.Id, a.ProcessingState.ToString(), a.ProcessingError))
            .FirstOrDefaultAsync(ct);
        return status is null ? NotFound() : Ok(status);
    }

    private static bool IsMultipart(string? contentType, out string boundary)
    {
        boundary = string.Empty;
        if (string.IsNullOrEmpty(contentType)) return false;
        var media = MediaTypeHeaderValue.Parse(contentType);
        if (!media.MediaType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
            return false;
        boundary = HeaderUtilities.RemoveQuotes(media.Boundary).ToString();
        return !string.IsNullOrEmpty(boundary);
    }

    /// <summary>
    /// Strip path components from the client-supplied filename. The
    /// filesystem key is server-generated regardless; this only cleans the
    /// human-readable display name.
    /// </summary>
    private static string SanitiseFileName(string raw)
    {
        var name = Path.GetFileName(raw);
        if (string.IsNullOrWhiteSpace(name)) name = "upload.mp4";
        if (name.Length > 200) name = name[^200..];
        return name;
    }
}
