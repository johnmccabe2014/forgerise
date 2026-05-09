using ForgeRise.Api.Welfare;

namespace ForgeRise.Api.Sessions;

public sealed record PlayerReadiness(Guid PlayerId, SafeCategory Category);

public sealed record SessionPlanContext(
    Guid TeamId,
    string? FocusOverride,
    string? PreviousSessionFocus,
    string? PreviousSessionReview,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<PlayerReadiness> Readiness,
    bool HasRecentSelfIncident = false);

public sealed record PlanBlock(string Block, string Title, int DurationMinutes, string Intent, string Intensity);

public sealed record GeneratedPlan(
    string Focus,
    string Summary,
    IReadOnlyList<PlanBlock> Blocks,
    IReadOnlyList<PlayerReadiness> ReadinessSnapshot,
    IReadOnlyList<DrillRecommendation> Recommendations);

public interface ISessionPlanGenerator
{
    Task<GeneratedPlan> GenerateAsync(SessionPlanContext context, CancellationToken ct = default);
}
