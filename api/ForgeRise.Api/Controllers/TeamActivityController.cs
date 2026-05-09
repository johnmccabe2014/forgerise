using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Welfare;
using ForgeRise.Api.WelfareModule;
using ForgeRise.Api.WelfareModule.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Controllers;

/// <summary>
/// Team activity feed: read-only join over recent player-driven events
/// (self-submitted check-ins, self-reported incidents, invite redemptions).
/// No new schema — purely an aggregation surface so coaches can see what
/// happened since they last looked.
/// </summary>
[ApiController]
[Authorize]
[Route("teams/{teamId:guid}")]
public sealed class TeamActivityController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public TeamActivityController(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    [HttpGet("activity")]
    public async Task<IActionResult> List(
        Guid teamId,
        [FromQuery] DateTimeOffset? since,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var cutoff = since ?? _time.GetUtcNow().AddDays(-7);
        var cap = Math.Clamp(limit ?? 20, 1, 100);

        // Player ids on this team — pre-resolved so each subquery is a
        // narrow, indexed lookup.
        var playerNames = await _db.Players
            .Where(p => p.TeamId == teamId && p.DeletedAt == null)
            .Select(p => new { p.Id, p.DisplayName })
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);
        if (playerNames.Count == 0) return Ok(Array.Empty<TeamActivityEventDto>());

        var ids = playerNames.Keys.ToArray();

        var checkins = await _db.WellnessCheckIns
            .Where(c => ids.Contains(c.PlayerId)
                && c.SubmittedBySelf
                && c.CreatedAt >= cutoff)
            .OrderByDescending(c => c.CreatedAt)
            .Take(cap)
            .Select(c => new
            {
                c.Id,
                c.PlayerId,
                At = c.CreatedAt,
                c.Category,
            })
            .ToListAsync(ct);

        var incidents = await _db.IncidentReports
            .Where(i => ids.Contains(i.PlayerId)
                && i.SubmittedBySelf
                && i.DeletedAt == null
                && i.CreatedAt >= cutoff)
            .OrderByDescending(i => i.CreatedAt)
            .Take(cap)
            .Select(i => new
            {
                i.Id,
                i.PlayerId,
                At = i.CreatedAt,
                i.Severity,
                i.Summary,
                Acknowledged = i.AcknowledgedAt != null,
            })
            .ToListAsync(ct);

        var redemptions = await _db.PlayerLinks
            .Where(l => ids.Contains(l.PlayerId) && l.ClaimedAt >= cutoff)
            .OrderByDescending(l => l.ClaimedAt)
            .Take(cap)
            .Select(l => new
            {
                l.PlayerId,
                At = l.ClaimedAt,
            })
            .ToListAsync(ct);

        var events = new List<TeamActivityEventDto>();
        foreach (var c in checkins)
        {
            events.Add(new TeamActivityEventDto(
                TeamActivityKinds.CheckInSelfSubmitted,
                c.At,
                c.PlayerId,
                playerNames.GetValueOrDefault(c.PlayerId, "Unknown"),
                c.Id,
                c.Category,
                SafeCategoryLabels.Label(c.Category),
                Severity: null,
                Summary: null,
                Acknowledged: null));
        }
        foreach (var i in incidents)
        {
            events.Add(new TeamActivityEventDto(
                TeamActivityKinds.IncidentSelfReported,
                i.At,
                i.PlayerId,
                playerNames.GetValueOrDefault(i.PlayerId, "Unknown"),
                i.Id,
                Category: null,
                CategoryLabel: null,
                i.Severity,
                i.Summary,
                i.Acknowledged));
        }
        foreach (var r in redemptions)
        {
            events.Add(new TeamActivityEventDto(
                TeamActivityKinds.InviteRedeemed,
                r.At,
                r.PlayerId,
                playerNames.GetValueOrDefault(r.PlayerId, "Unknown"),
                SubjectId: null,
                Category: null,
                CategoryLabel: null,
                Severity: null,
                Summary: null,
                Acknowledged: null));
        }

        return Ok(events.OrderByDescending(e => e.At).Take(cap).ToList());
    }

    /// <summary>
    /// Per-coach unread badge state. Counts player-driven events newer than
    /// the caller's last seen watermark. Capped at 99 (UI renders "99+").
    /// </summary>
    [HttpGet("activity/seen")]
    public async Task<IActionResult> GetSeen(Guid teamId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        var seen = await _db.TeamActivitySeens
            .FirstOrDefaultAsync(s => s.TeamId == teamId && s.UserId == userId, ct);
        var cutoff = seen?.LastSeenAt ?? DateTimeOffset.MinValue;

        var unread = await CountUnreadAsync(teamId, cutoff, ct);
        return Ok(new TeamActivitySeenDto(seen?.LastSeenAt, unread));
    }

    /// <summary>
    /// Mark the activity feed as read up to "now" for the calling coach.
    /// Idempotent upsert keyed by (TeamId, UserId).
    /// </summary>
    [HttpPost("activity/seen")]
    public async Task<IActionResult> MarkSeen(Guid teamId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        var now = _time.GetUtcNow();
        var seen = await _db.TeamActivitySeens
            .FirstOrDefaultAsync(s => s.TeamId == teamId && s.UserId == userId, ct);
        if (seen is null)
        {
            seen = new Data.Entities.TeamActivitySeen
            {
                TeamId = teamId,
                UserId = userId.Value,
                LastSeenAt = now,
            };
            _db.TeamActivitySeens.Add(seen);
        }
        else
        {
            seen.LastSeenAt = now;
        }
        await _db.SaveChangesAsync(ct);

        return Ok(new TeamActivitySeenDto(seen.LastSeenAt, 0));
    }

    private async Task<int> CountUnreadAsync(Guid teamId, DateTimeOffset cutoff, CancellationToken ct)
    {
        const int cap = 99;
        var ids = await _db.Players
            .Where(p => p.TeamId == teamId && p.DeletedAt == null)
            .Select(p => p.Id)
            .ToArrayAsync(ct);
        if (ids.Length == 0) return 0;

        var checkins = await _db.WellnessCheckIns
            .CountAsync(c => ids.Contains(c.PlayerId)
                && c.SubmittedBySelf
                && c.CreatedAt > cutoff, ct);
        if (checkins >= cap) return cap;

        var incidents = await _db.IncidentReports
            .CountAsync(i => ids.Contains(i.PlayerId)
                && i.SubmittedBySelf
                && i.DeletedAt == null
                && i.CreatedAt > cutoff, ct);
        if (checkins + incidents >= cap) return cap;

        var redemptions = await _db.PlayerLinks
            .CountAsync(l => ids.Contains(l.PlayerId) && l.ClaimedAt > cutoff, ct);

        return Math.Min(cap, checkins + incidents + redemptions);
    }
}
