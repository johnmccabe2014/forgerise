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

    private static IncidentSummaryDto ToSummary(IncidentReport i, string? acknowledgedByDisplayName = null) =>
        new(i.Id, i.PlayerId, i.OccurredAt, i.Severity, i.Summary, i.SubmittedBySelf, i.AcknowledgedAt, acknowledgedByDisplayName);

    [HttpGet("incidents")]
    public async Task<IActionResult> List(Guid teamId, [FromQuery] string? status, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var q = _db.IncidentReports
            .Where(i => i.DeletedAt == null
                     && _db.Players.Any(p => p.Id == i.PlayerId && p.TeamId == teamId));

        // Filter shape: "unread" = self-reported and not yet acknowledged;
        // "acknowledged" = acknowledged at any time; default "all" returns everything.
        switch ((status ?? "all").ToLowerInvariant())
        {
            case "unread":
                q = q.Where(i => i.SubmittedBySelf && i.AcknowledgedAt == null);
                break;
            case "acknowledged":
                q = q.Where(i => i.AcknowledgedAt != null);
                break;
        }

        var rows = await q.OrderByDescending(i => i.OccurredAt).ToListAsync(ct);

        // Resolve acknowledger names in one query rather than one-per-row.
        var ackUserIds = rows
            .Where(r => r.AcknowledgedByUserId is not null)
            .Select(r => r.AcknowledgedByUserId!.Value)
            .Distinct()
            .ToList();
        var nameById = ackUserIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users
                .Where(u => ackUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return Ok(rows.Select(r =>
        {
            var name = r.AcknowledgedByUserId is { } id && nameById.TryGetValue(id, out var n) ? n : null;
            return ToSummary(r, name);
        }));
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

        return Ok(rows.Select(r => ToSummary(r)));
    }

    [HttpPost("players/{playerId:guid}/incidents")]
    public async Task<IActionResult> Create(Guid teamId, Guid playerId, [FromBody] CreateIncidentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var actor = User.TryGetUserId()!.Value;
        var now = _time.GetUtcNow();
        var incident = new IncidentReport
        {
            PlayerId = playerId,
            RecordedByUserId = actor,
            OccurredAt = request.OccurredAt ?? now,
            Severity = request.Severity,
            Summary = request.Summary.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes,
            // Coach-recorded incidents are acknowledged at creation; only
            // player-submitted reports need explicit triage.
            AcknowledgedAt = now,
            AcknowledgedByUserId = actor,
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

    [HttpPost("players/{playerId:guid}/incidents/{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid teamId, Guid playerId, Guid id, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var incident = await _db.IncidentReports.FirstOrDefaultAsync(
            i => i.Id == id && i.PlayerId == playerId && i.DeletedAt == null, ct);
        if (incident is null) return NotFound();

        var actor = User.TryGetUserId()!.Value;
        if (incident.AcknowledgedAt is null)
        {
            incident.AcknowledgedAt = _time.GetUtcNow();
            incident.AcknowledgedByUserId = actor;
            _db.WelfareAuditLogs.Add(new WelfareAuditLog
            {
                ActorUserId = actor,
                PlayerId = playerId,
                SubjectId = id,
                Action = WelfareAuditAction.AcknowledgeIncident,
                At = _time.GetUtcNow(),
            });
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("welfare.incident.acknowledged {IncidentId} {PlayerId} {ActorUserId}", id, playerId, actor);
        }

        return Ok(ToSummary(incident));
    }
}
