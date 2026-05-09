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
[Route("teams/{teamId:guid}/sessions")]
public sealed class SessionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SessionsController> _log;
    private readonly TimeProvider _time;

    public SessionsController(AppDbContext db, ILogger<SessionsController> log, TimeProvider time)
    {
        _db = db;
        _log = log;
        _time = time;
    }

    private static SessionDto ToDto(Session s) =>
        new(s.Id, s.TeamId, s.ScheduledAt, s.DurationMinutes, s.Type, s.Location, s.Focus,
            s.ReviewNotes, s.ReviewedAt, s.CreatedAt, s.SourceSessionPlanId);

    [HttpGet]
    public async Task<IActionResult> List(Guid teamId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var rows = await _db.Sessions
            .Where(s => s.TeamId == teamId)
            .OrderByDescending(s => s.ScheduledAt)
            .ToListAsync(ct);
        return Ok(rows.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid teamId, [FromBody] CreateSessionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var session = new Session
        {
            TeamId = teamId,
            ScheduledAt = request.ScheduledAt,
            DurationMinutes = request.DurationMinutes,
            Type = request.Type,
            Location = request.Location,
            Focus = request.Focus,
            CreatedByUserId = User.TryGetUserId()!.Value,
            CreatedAt = _time.GetUtcNow(),
        };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("sessions.created {SessionId} {TeamId}", session.Id, teamId);
        return CreatedAtAction(nameof(Get), new { teamId, id = session.Id }, ToDto(session));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid teamId, Guid id, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == id && s.TeamId == teamId, ct);
        if (session is null) return NotFound();
        return Ok(ToDto(session));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid teamId, Guid id, [FromBody] UpdateSessionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == id && s.TeamId == teamId, ct);
        if (session is null) return NotFound();

        session.ScheduledAt = request.ScheduledAt;
        session.DurationMinutes = request.DurationMinutes;
        session.Type = request.Type;
        session.Location = request.Location;
        session.Focus = request.Focus;
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(session));
    }

    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid teamId, Guid id, [FromBody] ReviewSessionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == id && s.TeamId == teamId, ct);
        if (session is null) return NotFound();

        session.ReviewNotes = request.ReviewNotes.Trim();
        session.ReviewedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("sessions.reviewed {SessionId} {TeamId}", id, teamId);
        return Ok(ToDto(session));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid teamId, Guid id, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == id && s.TeamId == teamId, ct);
        if (session is null) return NotFound();

        session.DeletedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("sessions.deleted {SessionId} {TeamId}", id, teamId);
        return NoContent();
    }
}
