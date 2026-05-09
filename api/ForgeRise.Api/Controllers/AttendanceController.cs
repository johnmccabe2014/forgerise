using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Sessions.Contracts;
using ForgeRise.Api.WelfareModule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Controllers;

[ApiController]
[Authorize]
[Route("teams/{teamId:guid}/sessions/{sessionId:guid}/attendance")]
public sealed class AttendanceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AttendanceController> _log;
    private readonly TimeProvider _time;

    public AttendanceController(AppDbContext db, ILogger<AttendanceController> log, TimeProvider time)
    {
        _db = db;
        _log = log;
        _time = time;
    }

    private async Task<(Session? session, IActionResult? err)> ResolveSession(Guid teamId, Guid sessionId, CancellationToken ct)
    {
        var (_, gate) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (gate is not null) return (null, gate);
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.TeamId == teamId, ct);
        if (session is null) return (null, NotFound());
        return (session, null);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid teamId, Guid sessionId, CancellationToken ct)
    {
        var (_, err) = await ResolveSession(teamId, sessionId, ct);
        if (err is not null) return err;

        var roster = await _db.Players
            .Where(p => p.TeamId == teamId)
            .OrderBy(p => p.DisplayName)
            .Select(p => new { p.Id, p.DisplayName })
            .ToListAsync(ct);

        var existing = await _db.AttendanceRecords
            .Where(a => a.SessionId == sessionId)
            .ToDictionaryAsync(a => a.PlayerId, ct);

        var rows = roster.Select(p =>
        {
            if (existing.TryGetValue(p.Id, out var rec))
                return new AttendanceRowDto(p.Id, p.DisplayName, rec.Status, rec.Note, rec.RecordedAt);
            return new AttendanceRowDto(p.Id, p.DisplayName, AttendanceStatus.Absent, null, null);
        }).ToList();

        return Ok(rows);
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(Guid teamId, Guid sessionId, [FromBody] AttendanceBulkUpsertRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var (_, err) = await ResolveSession(teamId, sessionId, ct);
        if (err is not null) return err;

        var teamPlayerIds = await _db.Players
            .Where(p => p.TeamId == teamId)
            .Select(p => p.Id)
            .ToHashSetAsync(ct);

        var stranger = request.Items.FirstOrDefault(i => !teamPlayerIds.Contains(i.PlayerId));
        if (stranger is not null)
        {
            ModelState.AddModelError(nameof(request.Items), $"Player {stranger.PlayerId} is not on this team.");
            return ValidationProblem(ModelState);
        }

        var existing = await _db.AttendanceRecords
            .Where(a => a.SessionId == sessionId)
            .ToDictionaryAsync(a => a.PlayerId, ct);

        var actor = User.TryGetUserId()!.Value;
        var now = _time.GetUtcNow();
        foreach (var item in request.Items)
        {
            if (existing.TryGetValue(item.PlayerId, out var rec))
            {
                rec.Status = item.Status;
                rec.Note = item.Note;
                rec.RecordedAt = now;
                rec.RecordedByUserId = actor;
            }
            else
            {
                _db.AttendanceRecords.Add(new AttendanceRecord
                {
                    SessionId = sessionId,
                    PlayerId = item.PlayerId,
                    Status = item.Status,
                    Note = item.Note,
                    RecordedAt = now,
                    RecordedByUserId = actor,
                });
            }
        }
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("attendance.upserted {SessionId} {TeamId} {Count}", sessionId, teamId, request.Items.Count);
        return await List(teamId, sessionId, ct);
    }
}
