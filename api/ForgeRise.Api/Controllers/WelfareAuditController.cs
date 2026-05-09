using ForgeRise.Api.Data;
using ForgeRise.Api.WelfareModule;
using ForgeRise.Api.WelfareModule.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Controllers;

[ApiController]
[Authorize]
[Route("teams/{teamId:guid}/welfare-audit")]
public sealed class WelfareAuditController : ControllerBase
{
    private readonly AppDbContext _db;
    public WelfareAuditController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(Guid teamId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var rows = await _db.WelfareAuditLogs
            .Where(a => _db.Players.Any(p => p.Id == a.PlayerId && p.TeamId == teamId))
            .OrderByDescending(a => a.At)
            .Take(500)
            .ToListAsync(ct);

        // Resolve actor + player display names in two single round-trips so
        // the page can render "Coach acknowledged Player" without N+1.
        var actorIds = rows.Select(r => r.ActorUserId).Distinct().ToArray();
        var playerIds = rows.Select(r => r.PlayerId).Distinct().ToArray();
        var actorById = actorIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var playerById = playerIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _db.Players
                .Where(p => playerIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);

        return Ok(rows.Select(a => new AuditEntryDto(
            a.Id, a.ActorUserId, a.PlayerId, a.SubjectId, a.Action, a.At,
            actorById.TryGetValue(a.ActorUserId, out var an) ? an : null,
            playerById.TryGetValue(a.PlayerId, out var pn) ? pn : null)));
    }
}
