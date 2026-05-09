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
}
