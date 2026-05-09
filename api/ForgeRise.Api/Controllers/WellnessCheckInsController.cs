using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Welfare;
using ForgeRise.Api.WelfareModule;
using ForgeRise.Api.WelfareModule.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Controllers;

[ApiController]
[Authorize]
[Route("teams/{teamId:guid}")]
public sealed class WellnessCheckInsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<WellnessCheckInsController> _log;
    private readonly TimeProvider _time;

    public WellnessCheckInsController(AppDbContext db, ILogger<WellnessCheckInsController> log, TimeProvider time)
    {
        _db = db;
        _log = log;
        _time = time;
    }

    private static CheckInSummaryDto ToSummary(WellnessCheckIn c) =>
        new(c.Id, c.PlayerId, c.AsOf, c.Category, SafeCategoryLabels.Label(c.Category), c.SubmittedBySelf);

    private static CheckInRawDto ToRaw(WellnessCheckIn c) => new(
        c.Id, c.PlayerId, c.AsOf,
        c.SleepHours, c.SorenessScore, c.MoodScore, c.StressScore, c.FatigueScore, c.InjuryNotes,
        c.Category, c.RawPurgedAt);

    [HttpGet("readiness")]
    public async Task<IActionResult> TeamReadiness(Guid teamId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        // Latest check-in per player, then project to safe view only.
        var rows = await _db.Players
            .Where(p => p.TeamId == teamId)
            .Select(p => new
            {
                p.Id,
                p.DisplayName,
                Latest = _db.WellnessCheckIns
                    .Where(c => c.PlayerId == p.Id)
                    .OrderByDescending(c => c.AsOf)
                    .Select(c => new { c.Category, c.AsOf, c.SubmittedBySelf })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var dto = rows
            .Where(r => r.Latest is not null)
            .Select(r => new TeamReadinessDto(
                r.Id, r.DisplayName, r.Latest!.Category,
                SafeCategoryLabels.Label(r.Latest.Category), r.Latest.AsOf,
                r.Latest.SubmittedBySelf))
            .ToList();

        return Ok(dto);
    }

    [HttpGet("players/{playerId:guid}/checkins")]
    public async Task<IActionResult> List(Guid teamId, Guid playerId, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var checkins = await _db.WellnessCheckIns
            .Where(c => c.PlayerId == playerId)
            .OrderByDescending(c => c.AsOf)
            .ToListAsync(ct);

        return Ok(checkins.Select(ToSummary));
    }

    [HttpPost("players/{playerId:guid}/checkins")]
    public async Task<IActionResult> Create(Guid teamId, Guid playerId, [FromBody] CreateCheckInRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (_, player, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var userId = User.TryGetUserId()!.Value;
        var category = ReadinessCategorizer.Categorize(
            request.SleepHours, request.SorenessScore, request.MoodScore,
            request.StressScore, request.FatigueScore);

        var checkin = new WellnessCheckIn
        {
            PlayerId = playerId,
            RecordedByUserId = userId,
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
        await _db.SaveChangesAsync(ct);

        // SAFE log: id + category only. NEVER raw scores or notes.
        _log.LogInformation("welfare.checkin.recorded {CheckInId} {PlayerId} {Category}",
            checkin.Id, playerId, category);

        return CreatedAtAction(nameof(Get), new { teamId, playerId, id = checkin.Id }, ToSummary(checkin));
    }

    [HttpGet("players/{playerId:guid}/checkins/{id:guid}")]
    public async Task<IActionResult> Get(Guid teamId, Guid playerId, Guid id, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var checkin = await _db.WellnessCheckIns.FirstOrDefaultAsync(c => c.Id == id && c.PlayerId == playerId, ct);
        if (checkin is null) return NotFound();
        return Ok(ToSummary(checkin));
    }

    [HttpGet("players/{playerId:guid}/checkins/{id:guid}/raw")]
    public async Task<IActionResult> ReadRaw(Guid teamId, Guid playerId, Guid id, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var checkin = await _db.WellnessCheckIns.FirstOrDefaultAsync(c => c.Id == id && c.PlayerId == playerId, ct);
        if (checkin is null) return NotFound();

        var actor = User.TryGetUserId()!.Value;
        _db.WelfareAuditLogs.Add(new WelfareAuditLog
        {
            ActorUserId = actor,
            PlayerId = playerId,
            SubjectId = id,
            Action = WelfareAuditAction.ReadRawCheckIn,
            At = _time.GetUtcNow(),
        });
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("welfare.checkin.raw_read {CheckInId} {PlayerId} {ActorUserId}", id, playerId, actor);
        return Ok(ToRaw(checkin));
    }

    [HttpPost("players/{playerId:guid}/checkins/{id:guid}/purge-raw")]
    public async Task<IActionResult> PurgeRaw(Guid teamId, Guid playerId, Guid id, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var checkin = await _db.WellnessCheckIns.FirstOrDefaultAsync(c => c.Id == id && c.PlayerId == playerId, ct);
        if (checkin is null) return NotFound();

        checkin.SleepHours = null;
        checkin.SorenessScore = null;
        checkin.MoodScore = null;
        checkin.StressScore = null;
        checkin.FatigueScore = null;
        checkin.InjuryNotes = null;
        checkin.RawPurgedAt = _time.GetUtcNow();

        var actor = User.TryGetUserId()!.Value;
        _db.WelfareAuditLogs.Add(new WelfareAuditLog
        {
            ActorUserId = actor,
            PlayerId = playerId,
            SubjectId = id,
            Action = WelfareAuditAction.PurgeRawCheckIn,
            At = _time.GetUtcNow(),
        });
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("welfare.checkin.raw_purged {CheckInId} {PlayerId} {ActorUserId}", id, playerId, actor);
        return NoContent();
    }

    [HttpDelete("players/{playerId:guid}/checkins/{id:guid}")]
    public async Task<IActionResult> Delete(Guid teamId, Guid playerId, Guid id, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var checkin = await _db.WellnessCheckIns.FirstOrDefaultAsync(c => c.Id == id && c.PlayerId == playerId, ct);
        if (checkin is null) return NotFound();

        checkin.DeletedAt = _time.GetUtcNow();
        var actor = User.TryGetUserId()!.Value;
        _db.WelfareAuditLogs.Add(new WelfareAuditLog
        {
            ActorUserId = actor,
            PlayerId = playerId,
            SubjectId = id,
            Action = WelfareAuditAction.DeleteCheckIn,
            At = _time.GetUtcNow(),
        });
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("welfare.checkin.deleted {CheckInId} {PlayerId} {ActorUserId}", id, playerId, actor);
        return NoContent();
    }
}
