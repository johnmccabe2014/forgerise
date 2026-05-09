using System.Text.Json;
using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Sessions;
using ForgeRise.Api.Sessions.Contracts;
using ForgeRise.Api.Welfare;
using ForgeRise.Api.WelfareModule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Controllers;

[ApiController]
[Authorize]
[Route("teams/{teamId:guid}/session-plans")]
public sealed class SessionPlansController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly ISessionPlanGenerator _generator;
    private readonly ILogger<SessionPlansController> _log;
    private readonly TimeProvider _time;

    public SessionPlansController(
        AppDbContext db,
        ISessionPlanGenerator generator,
        ILogger<SessionPlansController> log,
        TimeProvider time)
    {
        _db = db;
        _generator = generator;
        _log = log;
        _time = time;
    }

    private static SessionPlanDto Materialise(SessionPlan plan)
    {
        var blocks = JsonSerializer.Deserialize<List<SessionPlanBlockDto>>(plan.PlanJson, JsonOpts)
                     ?? new List<SessionPlanBlockDto>();
        var snapshot = JsonSerializer.Deserialize<List<SessionPlanReadinessRow>>(plan.ReadinessSnapshotJson, JsonOpts)
                       ?? new List<SessionPlanReadinessRow>();
        return new SessionPlanDto(plan.Id, plan.TeamId, plan.GeneratedAt, plan.BasedOnSessionId,
            plan.Focus, plan.Summary, blocks, snapshot);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid teamId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var plans = await _db.SessionPlans
            .Where(p => p.TeamId == teamId)
            .OrderByDescending(p => p.GeneratedAt)
            .ToListAsync(ct);

        return Ok(plans.Select(Materialise));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid teamId, Guid id, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var plan = await _db.SessionPlans.FirstOrDefaultAsync(p => p.Id == id && p.TeamId == teamId, ct);
        if (plan is null) return NotFound();
        return Ok(Materialise(plan));
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(Guid teamId, [FromBody] GenerateSessionPlanRequest? request, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        request ??= new GenerateSessionPlanRequest();

        // Pick the basis session: explicit override, else the most recent reviewed session.
        Session? basis = null;
        if (request.BasedOnSessionId is { } basisId)
        {
            basis = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == basisId && s.TeamId == teamId, ct);
            if (basis is null) return NotFound();
        }
        else
        {
            basis = await _db.Sessions
                .Where(s => s.TeamId == teamId && s.ReviewedAt != null)
                .OrderByDescending(s => s.ReviewedAt)
                .FirstOrDefaultAsync(ct);
        }

        // Build readiness snapshot — ids + SafeCategory only. NEVER raw welfare fields.
        var roster = await _db.Players
            .Where(p => p.TeamId == teamId)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var snapshot = new List<PlayerReadiness>();
        foreach (var pid in roster)
        {
            var latest = await _db.WellnessCheckIns
                .Where(c => c.PlayerId == pid)
                .OrderByDescending(c => c.AsOf)
                .Select(c => (SafeCategory?)c.Category)
                .FirstOrDefaultAsync(ct);
            if (latest is not null) snapshot.Add(new PlayerReadiness(pid, latest.Value));
        }

        var ctx = new SessionPlanContext(
            TeamId: teamId,
            FocusOverride: request.Focus,
            PreviousSessionFocus: basis?.Focus,
            PreviousSessionReview: basis?.ReviewNotes,
            GeneratedAt: _time.GetUtcNow(),
            Readiness: snapshot);

        var generated = await _generator.GenerateAsync(ctx, ct);

        var entity = new SessionPlan
        {
            TeamId = teamId,
            GeneratedAt = _time.GetUtcNow(),
            GeneratedByUserId = User.TryGetUserId()!.Value,
            BasedOnSessionId = basis?.Id,
            Focus = generated.Focus,
            Summary = generated.Summary,
            PlanJson = JsonSerializer.Serialize(
                generated.Blocks.Select(b => new SessionPlanBlockDto(b.Block, b.Title, b.DurationMinutes, b.Intent, b.Intensity)),
                JsonOpts),
            ReadinessSnapshotJson = JsonSerializer.Serialize(
                generated.ReadinessSnapshot.Select(r => new SessionPlanReadinessRow(r.PlayerId, r.Category)),
                JsonOpts),
        };
        _db.SessionPlans.Add(entity);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("session_plan.generated {PlanId} {TeamId} {RosterCount}", entity.Id, teamId, snapshot.Count);

        return CreatedAtAction(nameof(Get), new { teamId, id = entity.Id }, Materialise(entity));
    }
}
