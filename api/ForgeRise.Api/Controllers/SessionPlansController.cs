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
        var recs = JsonSerializer.Deserialize<List<SessionPlanRecommendationDto>>(plan.RecommendationsJson, JsonOpts)
                   ?? new List<SessionPlanRecommendationDto>();
        return new SessionPlanDto(plan.Id, plan.TeamId, plan.GeneratedAt, plan.BasedOnSessionId,
            plan.Focus, plan.Summary, blocks, snapshot, recs, plan.RecentSelfIncidentCount,
            plan.AdoptedAt, plan.AdoptedSessionId, plan.PinnedAt);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid teamId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var plans = await _db.SessionPlans
            .Where(p => p.TeamId == teamId)
            // Pinned plans float to the top, then most recent first.
            .OrderByDescending(p => p.PinnedAt != null)
            .ThenByDescending(p => p.PinnedAt)
            .ThenByDescending(p => p.GeneratedAt)
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

        // Bias drill recommendations toward low-contact when any player on this team
        // has self-reported an incident in the last 14 days. Provenance only — never
        // raw welfare detail. Master prompt §9.
        var incidentCutoff = _time.GetUtcNow().AddDays(-14);
        var recentSelfIncidentCount = roster.Count == 0 ? 0 : await _db.IncidentReports
            .CountAsync(i => roster.Contains(i.PlayerId)
                       && i.SubmittedBySelf
                       && i.DeletedAt == null
                       && i.CreatedAt >= incidentCutoff, ct);
        var hasRecentSelfIncident = recentSelfIncidentCount > 0;

        // Per-team drill preferences let coaches steer the recommender without
        // touching the static catalogue: favourites get prioritised, excludes
        // are filtered out entirely.
        var prefs = await _db.TeamDrillPreferences
            .Where(p => p.TeamId == teamId)
            .Select(p => new { p.DrillId, p.Status })
            .ToListAsync(ct);
        var favourites = prefs
            .Where(p => p.Status == DrillPreferenceStatus.Favourite)
            .Select(p => p.DrillId)
            .ToHashSet(StringComparer.Ordinal);
        var excludes = prefs
            .Where(p => p.Status == DrillPreferenceStatus.Exclude)
            .Select(p => p.DrillId)
            .ToHashSet(StringComparer.Ordinal);

        var ctx = new SessionPlanContext(
            TeamId: teamId,
            FocusOverride: request.Focus,
            PreviousSessionFocus: basis?.Focus,
            PreviousSessionReview: basis?.ReviewNotes,
            GeneratedAt: _time.GetUtcNow(),
            Readiness: snapshot,
            HasRecentSelfIncident: hasRecentSelfIncident,
            FavouriteDrillIds: favourites,
            ExcludedDrillIds: excludes);

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
            RecommendationsJson = JsonSerializer.Serialize(
                generated.Recommendations.Select(r => new SessionPlanRecommendationDto(
                    r.DrillId, r.Title, r.Description, r.DurationMinutes, r.Rationale, r.Tags)),
                JsonOpts),
            RecentSelfIncidentCount = recentSelfIncidentCount,
        };
        _db.SessionPlans.Add(entity);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("session_plan.generated {PlanId} {TeamId} {RosterCount}", entity.Id, teamId, snapshot.Count);

        return CreatedAtAction(nameof(Get), new { teamId, id = entity.Id }, Materialise(entity));
    }

    [HttpPost("{id:guid}/adopt")]
    public async Task<IActionResult> Adopt(
        Guid teamId,
        Guid id,
        [FromBody] AdoptSessionPlanRequest request,
        CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var plan = await _db.SessionPlans.FirstOrDefaultAsync(p => p.Id == id && p.TeamId == teamId, ct);
        if (plan is null) return NotFound();
        if (plan.AdoptedSessionId is not null)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["plan"] = new[] { "Plan has already been adopted." },
            }));
        }

        // Build a short, coach-readable digest of the recommended drills so
        // they survive on the Session record even if the catalogue moves.
        var recs = JsonSerializer.Deserialize<List<SessionPlanRecommendationDto>>(plan.RecommendationsJson, JsonOpts)
                   ?? new List<SessionPlanRecommendationDto>();
        var notes = recs.Count == 0
            ? null
            : "Adopted from session plan. Drills:\n" + string.Join("\n",
                recs.Select(r => $"- {r.Title} ({r.DurationMinutes} min) — {r.Rationale}"));

        var now = _time.GetUtcNow();
        var actor = User.TryGetUserId()!.Value;
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            ScheduledAt = request.ScheduledAt,
            DurationMinutes = request.DurationMinutes,
            Type = request.Type,
            Location = request.Location,
            Focus = plan.Focus,
            ReviewNotes = notes,
            CreatedByUserId = actor,
            CreatedAt = now,
            SourceSessionPlanId = plan.Id,
        };
        _db.Sessions.Add(session);

        plan.AdoptedAt = now;
        plan.AdoptedByUserId = actor;
        plan.AdoptedSessionId = session.Id;
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("session_plan.adopted {PlanId} {SessionId} {TeamId}", plan.Id, session.Id, teamId);
        return Ok(Materialise(plan));
    }

    /// <summary>
    /// Toggle pin state on a plan. Pinned plans float to the top of the
    /// listing across regenerations.
    /// </summary>
    [HttpPost("{id:guid}/pin")]
    public async Task<IActionResult> TogglePin(Guid teamId, Guid id, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var plan = await _db.SessionPlans.FirstOrDefaultAsync(p => p.Id == id && p.TeamId == teamId, ct);
        if (plan is null) return NotFound();

        plan.PinnedAt = plan.PinnedAt is null ? _time.GetUtcNow() : null;
        await _db.SaveChangesAsync(ct);
        return Ok(Materialise(plan));
    }
}
