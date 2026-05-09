using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Welfare;
using ForgeRise.Api.WelfareModule.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Controllers;

/// <summary>
/// Self-service endpoints for the authenticated user, scoped to whichever
/// players they have claimed via <see cref="PlayerInvitesController.Redeem"/>.
/// Players see their own data only — never other players, never team-wide
/// rollups, never raw data for anyone else.
/// </summary>
[ApiController]
[Authorize]
[Route("me")]
public sealed class MeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<MeController> _log;
    private readonly TimeProvider _time;

    public MeController(AppDbContext db, ILogger<MeController> log, TimeProvider time)
    {
        _db = db;
        _log = log;
        _time = time;
    }

    private async Task<(Guid? userId, Player? player, IActionResult? error)> RequireLinkedPlayer(
        Guid playerId, CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        if (userId is null) return (null, null, Unauthorized());

        var link = await _db.PlayerLinks
            .Include(l => l.Player)
            .FirstOrDefaultAsync(l => l.PlayerId == playerId && l.UserId == userId, ct);
        if (link is null) return (userId, null, Forbid());
        if (link.Player.DeletedAt is not null) return (userId, null, NotFound());
        return (userId, link.Player, null);
    }

    [HttpGet("players")]
    public async Task<IActionResult> ListLinkedPlayers(CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        var rows = await _db.PlayerLinks
            .Where(l => l.UserId == userId)
            .Include(l => l.Player).ThenInclude(p => p.Team)
            .OrderBy(l => l.Player.Team.Name).ThenBy(l => l.Player.DisplayName)
            .ToListAsync(ct);

        var dto = rows
            .Where(l => l.Player.DeletedAt is null && l.Player.Team.DeletedAt is null)
            .Select(l => new MyLinkedPlayerDto(
                l.PlayerId,
                l.Player.DisplayName,
                l.Player.TeamId,
                l.Player.Team.Name,
                l.ClaimedAt))
            .ToList();
        return Ok(dto);
    }

    [HttpGet("players/{playerId:guid}/checkins")]
    public async Task<IActionResult> ListMyCheckIns(Guid playerId, CancellationToken ct)
    {
        var (_, _, err) = await RequireLinkedPlayer(playerId, ct);
        if (err is not null) return err;

        var checkins = await _db.WellnessCheckIns
            .Where(c => c.PlayerId == playerId)
            .OrderByDescending(c => c.AsOf)
            .ToListAsync(ct);

        return Ok(checkins.Select(ToDto));
    }

    [HttpPost("players/{playerId:guid}/checkins")]
    public async Task<IActionResult> CreateMyCheckIn(
        Guid playerId, [FromBody] CreateCheckInRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (userIdNullable, _, err) = await RequireLinkedPlayer(playerId, ct);
        if (err is not null) return err;
        var userId = userIdNullable!.Value;

        var category = ReadinessCategorizer.Categorize(
            request.SleepHours, request.SorenessScore, request.MoodScore,
            request.StressScore, request.FatigueScore);

        var checkin = new WellnessCheckIn
        {
            PlayerId = playerId,
            RecordedByUserId = userId,
            SubmittedBySelf = true,
            AsOf = request.AsOf ?? _time.GetUtcNow(),
            SleepHours = request.SleepHours,
            SorenessScore = request.SorenessScore,
            MoodScore = request.MoodScore,
            StressScore = request.StressScore,
            FatigueScore = request.FatigueScore,
            InjuryNotes = request.InjuryNotes,
            Category = category,
        };
        _db.WellnessCheckIns.Add(checkin);

        // Self-submission is itself a welfare event — log it for the audit
        // trail so coaches can see who submitted what (id + category only).
        _db.WelfareAuditLogs.Add(new WelfareAuditLog
        {
            ActorUserId = userId,
            PlayerId = playerId,
            SubjectId = checkin.Id,
            Action = WelfareAuditAction.SelfSubmitCheckIn,
            At = _time.GetUtcNow(),
        });
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("welfare.checkin.self_submitted {CheckInId} {PlayerId} {Category}",
            checkin.Id, playerId, category);

        return CreatedAtAction(nameof(GetMyCheckIn),
            new { playerId, id = checkin.Id }, ToDto(checkin));
    }

    [HttpGet("players/{playerId:guid}/checkins/{id:guid}")]
    public async Task<IActionResult> GetMyCheckIn(Guid playerId, Guid id, CancellationToken ct)
    {
        var (_, _, err) = await RequireLinkedPlayer(playerId, ct);
        if (err is not null) return err;

        var checkin = await _db.WellnessCheckIns
            .FirstOrDefaultAsync(c => c.Id == id && c.PlayerId == playerId, ct);
        if (checkin is null) return NotFound();
        return Ok(ToDto(checkin));
    }

    private static MyCheckInDto ToDto(WellnessCheckIn c) => new(
        c.Id, c.PlayerId, c.AsOf,
        c.SleepHours, c.SorenessScore, c.MoodScore, c.StressScore, c.FatigueScore, c.InjuryNotes,
        c.Category, SafeCategoryLabels.Label(c.Category), c.SubmittedBySelf);

    [HttpGet("players/{playerId:guid}/incidents")]
    public async Task<IActionResult> ListMyIncidents(Guid playerId, CancellationToken ct)
    {
        var (_, _, err) = await RequireLinkedPlayer(playerId, ct);
        if (err is not null) return err;

        var incidents = await _db.IncidentReports
            .Where(i => i.PlayerId == playerId && i.DeletedAt == null)
            .OrderByDescending(i => i.OccurredAt)
            .ToListAsync(ct);

        return Ok(incidents.Select(ToIncidentDto));
    }

    [HttpPost("players/{playerId:guid}/incidents")]
    public async Task<IActionResult> CreateMyIncident(
        Guid playerId, [FromBody] CreateSelfIncidentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // Self-reports are capped at Medium. High severity must come from a
        // coach so it goes through proper triage. Reject with a clear error
        // so the UI can surface "please contact your coach directly".
        if (request.Severity == IncidentSeverity.High)
        {
            ModelState.AddModelError(nameof(request.Severity), "self_high_severity_not_allowed");
            return ValidationProblem(ModelState);
        }

        var (userIdNullable, _, err) = await RequireLinkedPlayer(playerId, ct);
        if (err is not null) return err;
        var userId = userIdNullable!.Value;

        var incident = new IncidentReport
        {
            PlayerId = playerId,
            RecordedByUserId = userId,
            SubmittedBySelf = true,
            OccurredAt = request.OccurredAt ?? _time.GetUtcNow(),
            Severity = request.Severity,
            Summary = request.Summary.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes,
        };
        _db.IncidentReports.Add(incident);
        _db.WelfareAuditLogs.Add(new WelfareAuditLog
        {
            ActorUserId = userId,
            PlayerId = playerId,
            SubjectId = incident.Id,
            Action = WelfareAuditAction.SelfReportIncident,
            At = _time.GetUtcNow(),
        });
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("welfare.incident.self_reported {IncidentId} {PlayerId} {Severity}",
            incident.Id, playerId, incident.Severity);

        return CreatedAtAction(nameof(GetMyIncident),
            new { playerId, id = incident.Id }, ToIncidentDto(incident));
    }

    [HttpGet("players/{playerId:guid}/incidents/{id:guid}")]
    public async Task<IActionResult> GetMyIncident(Guid playerId, Guid id, CancellationToken ct)
    {
        var (_, _, err) = await RequireLinkedPlayer(playerId, ct);
        if (err is not null) return err;

        var incident = await _db.IncidentReports
            .FirstOrDefaultAsync(i => i.Id == id && i.PlayerId == playerId && i.DeletedAt == null, ct);
        if (incident is null) return NotFound();
        return Ok(ToIncidentDto(incident));
    }

    private static MyIncidentDto ToIncidentDto(IncidentReport i) =>
        new(i.Id, i.PlayerId, i.OccurredAt, i.Severity, i.Summary, i.Notes, i.SubmittedBySelf);
}
