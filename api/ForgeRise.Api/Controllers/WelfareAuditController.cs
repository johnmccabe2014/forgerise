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
            .Select(a => new AuditEntryDto(a.Id, a.ActorUserId, a.PlayerId, a.SubjectId, a.Action, a.At))
            .ToListAsync(ct);

        return Ok(rows);
    }
}
