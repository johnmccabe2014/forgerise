using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.WelfareModule;
using ForgeRise.Api.WelfareModule.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Controllers;

[ApiController]
[Authorize]
[Route("teams/{teamId:guid}")]
public sealed class IncidentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<IncidentsController> _log;
    private readonly TimeProvider _time;

    public IncidentsController(AppDbContext db, ILogger<IncidentsController> log, TimeProvider time)
    {
        _db = db;
        _log = log;
        _time = time;
    }

    private static IncidentSummaryDto ToSummary(IncidentReport i) =>
        new(i.Id, i.PlayerId, i.OccurredAt, i.Severity, i.Summary, i.SubmittedBySelf);

    [HttpGet("incidents")]
    public async Task<IActionResult> List(Guid teamId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var rows = await _db.IncidentReports
            .Where(i => _db.Players.Any(p => p.Id == i.PlayerId && p.TeamId == teamId))
            .OrderByDescending(i => i.OccurredAt)
            .ToListAsync(ct);

        return Ok(rows.Select(ToSummary));
    }

    [HttpGet("players/{playerId:guid}/incidents")]
    public async Task<IActionResult> ListForPlayer(Guid teamId, Guid playerId, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var rows = await _db.IncidentReports
            .Where(i => i.PlayerId == playerId)
            .OrderByDescending(i => i.OccurredAt)
            .ToListAsync(ct);

        return Ok(rows.Select(ToSummary));
    }

    [HttpPost("players/{playerId:guid}/incidents")]
    public async Task<IActionResult> Create(Guid teamId, Guid playerId, [FromBody] CreateIncidentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var actor = User.TryGetUserId()!.Value;
        var incident = new IncidentReport
        {
            PlayerId = playerId,
            RecordedByUserId = actor,
            OccurredAt = request.OccurredAt ?? _time.GetUtcNow(),
            Severity = request.Severity,
            Summary = request.Summary.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes,
        };
        _db.IncidentReports.Add(incident);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("welfare.incident.recorded {IncidentId} {PlayerId} {Severity} {ActorUserId}",
            incident.Id, playerId, incident.Severity, actor);

        return CreatedAtAction(nameof(Get), new { teamId, playerId, id = incident.Id }, ToSummary(incident));
    }

    [HttpGet("players/{playerId:guid}/incidents/{id:guid}")]
    public async Task<IActionResult> Get(Guid teamId, Guid playerId, Guid id, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var incident = await _db.IncidentReports.FirstOrDefaultAsync(i => i.Id == id && i.PlayerId == playerId, ct);
        if (incident is null) return NotFound();
        return Ok(ToSummary(incident));
    }

    [HttpGet("players/{playerId:guid}/incidents/{id:guid}/raw")]
    public async Task<IActionResult> ReadRaw(Guid teamId, Guid playerId, Guid id, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var incident = await _db.IncidentReports.FirstOrDefaultAsync(i => i.Id == id && i.PlayerId == playerId, ct);
        if (incident is null) return NotFound();

        var actor = User.TryGetUserId()!.Value;
        _db.WelfareAuditLogs.Add(new WelfareAuditLog
        {
            ActorUserId = actor,
            PlayerId = playerId,
            SubjectId = id,
            Action = WelfareAuditAction.ReadRawIncident,
            At = _time.GetUtcNow(),
        });
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("welfare.incident.raw_read {IncidentId} {PlayerId} {ActorUserId}", id, playerId, actor);

        return Ok(new IncidentRawDto(
            incident.Id, incident.PlayerId, incident.OccurredAt, incident.Severity,
            incident.Summary, incident.Notes, incident.RawPurgedAt));
    }

    [HttpPost("players/{playerId:guid}/incidents/{id:guid}/purge-raw")]
    public async Task<IActionResult> PurgeRaw(Guid teamId, Guid playerId, Guid id, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var incident = await _db.IncidentReports.FirstOrDefaultAsync(i => i.Id == id && i.PlayerId == playerId, ct);
        if (incident is null) return NotFound();

        incident.Notes = null;
        incident.RawPurgedAt = _time.GetUtcNow();

        var actor = User.TryGetUserId()!.Value;
        _db.WelfareAuditLogs.Add(new WelfareAuditLog
        {
            ActorUserId = actor,
            PlayerId = playerId,
            SubjectId = id,
            Action = WelfareAuditAction.PurgeRawIncident,
            At = _time.GetUtcNow(),
        });
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("welfare.incident.raw_purged {IncidentId} {PlayerId} {ActorUserId}", id, playerId, actor);
        return NoContent();
    }

    [HttpDelete("players/{playerId:guid}/incidents/{id:guid}")]
    public async Task<IActionResult> Delete(Guid teamId, Guid playerId, Guid id, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var incident = await _db.IncidentReports.FirstOrDefaultAsync(i => i.Id == id && i.PlayerId == playerId, ct);
        if (incident is null) return NotFound();

        incident.DeletedAt = _time.GetUtcNow();
        var actor = User.TryGetUserId()!.Value;
        _db.WelfareAuditLogs.Add(new WelfareAuditLog
        {
            ActorUserId = actor,
            PlayerId = playerId,
            SubjectId = id,
            Action = WelfareAuditAction.DeleteIncident,
            At = _time.GetUtcNow(),
        });
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("welfare.incident.deleted {IncidentId} {PlayerId} {ActorUserId}", id, playerId, actor);
        return NoContent();
    }
}
