using ForgeRise.Api.Data;
using ForgeRise.Api.Features.Video.Dtos;
using ForgeRise.Api.Features.Video.Options;
using ForgeRise.Api.WelfareModule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ForgeRise.Api.Features.Video.Endpoints;

/// <summary>
/// Video Intelligence module surface. V1 ships a single feature-flagged
/// listing endpoint that always returns an empty page. The whole controller
/// 404s when <c>Features:Video:Enabled</c> is false (default), so the
/// surface is invisible until a coach-visible slice ships.
/// </summary>
[ApiController]
[Authorize]
[Route("v1/teams/{teamId:guid}/videos")]
public sealed class VideosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly VideoFeatureOptions _options;

    public VideosController(AppDbContext db, IOptions<VideoFeatureOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid teamId, CancellationToken ct)
    {
        // Feature flag short-circuits BEFORE any auth/team probe so the
        // module looks nonexistent to clients when disabled.
        if (!_options.Enabled) return NotFound();

        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        // V2 returns real rows for the calling team; cross-team isolation
        // remains enforced by the WHERE clause and TeamScope above.
        var items = await _db.VideoAssets
            .Where(a => a.TeamId == teamId && a.DeletedAt == null)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new VideoListItemDto(
                a.Id,
                a.OriginalFileName,
                a.ProcessingState.ToString(),
                a.CreatedAt))
            .ToListAsync(ct);

        return Ok(new VideoListResponse(items, items.Count));
    }
}
