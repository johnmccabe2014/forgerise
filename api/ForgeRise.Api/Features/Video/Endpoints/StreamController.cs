using System.Text;
using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Features.Video.Options;
using ForgeRise.Api.Features.Video.Storage;
using ForgeRise.Api.WelfareModule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ForgeRise.Api.Features.Video.Endpoints;

/// <summary>
/// V2 playback. Two endpoints:
/// <list type="bullet">
///   <item><c>GET /v1/teams/{teamId}/videos/{videoId}/stream</c> — mints a
///   short-lived signed URL (302). Membership is re-checked here so a user
///   removed from the team cannot mint a new URL.</item>
///   <item><c>GET /v1/videos/blob?path=&amp;exp=&amp;v=&amp;sig=</c> — serves
///   bytes after verifying the HMAC. No <c>[Authorize]</c> on this one
///   because the signed URL IS the credential; we still re-check the
///   <c>v</c> user is currently a team member of the asset's team
///   (security review iter1, AC5).</item>
/// </list>
/// </summary>
[ApiController]
public sealed class StreamController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IObjectStore _store;
    private readonly LocalFsObjectStore _localStore;
    private readonly VideoFeatureOptions _flag;
    private readonly VideoSigningOptions _signing;
    private readonly TimeProvider _time;

    public StreamController(
        AppDbContext db,
        IObjectStore store,
        LocalFsObjectStore localStore,
        IOptions<VideoFeatureOptions> flag,
        IOptions<VideoSigningOptions> signing,
        TimeProvider time)
    {
        _db = db;
        _store = store;
        _localStore = localStore;
        _flag = flag.Value;
        _signing = signing.Value;
        _time = time;
    }

    [Authorize]
    [HttpGet("v1/teams/{teamId:guid}/videos/{videoId:guid}/stream")]
    public async Task<IActionResult> Mint(Guid teamId, Guid videoId, CancellationToken ct)
    {
        if (!_flag.Enabled) return NotFound();
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;
        var userId = User.TryGetUserId()!.Value;

        var asset = await _db.VideoAssets
            .Where(a => a.TeamId == teamId && a.Id == videoId && a.DeletedAt == null)
            .Select(a => new { a.StoragePath })
            .FirstOrDefaultAsync(ct);
        if (asset is null) return NotFound();

        var expiresAt = _time.GetUtcNow().Add(_signing.DefaultUrlTtl);
        var url = await _store.GetSignedReadUrlAsync(asset.StoragePath, userId, expiresAt, ct);

        // Stop the URL leaking through Referer/cache. Use indexer to
        // OVERRIDE any value set by SecurityHeadersMiddleware (which sets
        // a less-strict policy for the whole site).
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers.CacheControl = "private, no-store";
        return Redirect(url.ToString());
    }

    [HttpGet("v1/videos/blob")]
    public async Task<IActionResult> Blob(
        [FromQuery] string path,
        [FromQuery] long exp,
        [FromQuery] Guid v,
        [FromQuery] string sig,
        CancellationToken ct)
    {
        if (!_flag.Enabled) return NotFound();

        var secret = Encoding.UTF8.GetBytes(_signing.SigningSecret ?? string.Empty);
        var now = _time.GetUtcNow();

        if (DateTimeOffset.FromUnixTimeSeconds(exp) <= now)
        {
            return Problem(statusCode: 410, type: "signature_expired");
        }
        if (!SignedUrl.Verify(path, v, exp, sig ?? string.Empty, secret, now))
        {
            return Problem(statusCode: 401, type: "signature_invalid");
        }

        var asset = await _db.VideoAssets
            .Where(a => a.StoragePath == path && a.DeletedAt == null)
            .Select(a => new { a.TeamId, a.MimeType })
            .FirstOrDefaultAsync(ct);
        if (asset is null) return NotFound();

        // Re-check membership at request time so revocation flows through.
        var stillMember = await _db.TeamMemberships
            .AnyAsync(m => m.TeamId == asset.TeamId && m.UserId == v, ct);
        if (!stillMember) return Forbid();

        var full = _localStore.OpenForReadOrNull(path);
        if (full is null) return NotFound();

        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers.CacheControl = "private, no-store";
        var fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        return File(fs, asset.MimeType, enableRangeProcessing: true);
    }
}
