using ForgeRise.Api.Welfare;

namespace ForgeRise.Api.Sessions;

/// <summary>
/// Deterministic, in-process session-plan generator. Reads only safe inputs
/// (player ids + SafeCategory + coach-authored focus/review text) and returns
/// a fixed-shape five-block plan whose intensity bucket is driven by the
/// team-wide readiness mix.
///
/// Master prompt §4 (provider abstraction): replace this with an OpenAI/Azure
/// implementation by registering a different <see cref="ISessionPlanGenerator"/>.
/// </summary>
public sealed class HeuristicSessionPlanGenerator : ISessionPlanGenerator
{
    private const string DefaultFocus = "General conditioning + skills";

    public Task<GeneratedPlan> GenerateAsync(SessionPlanContext context, CancellationToken ct = default)
    {
        var intensity = PickIntensity(context.Readiness);
        var focus = !string.IsNullOrWhiteSpace(context.FocusOverride)
            ? context.FocusOverride!.Trim()
            : !string.IsNullOrWhiteSpace(context.PreviousSessionFocus)
                ? context.PreviousSessionFocus!.Trim()
                : DefaultFocus;

        var blocks = BuildBlocks(intensity, focus);
        var summary = BuildSummary(intensity, focus, context);
        var recommendations = DrillRecommender.Recommend(intensity, focus, context.HasRecentSelfIncident);

        return Task.FromResult(new GeneratedPlan(focus, summary, blocks, context.Readiness, recommendations));
    }

    internal static string PickIntensity(IReadOnlyList<PlayerReadiness> readiness)
    {
        if (readiness.Count == 0) return "Standard";

        var total = (double)readiness.Count;
        var recovery = readiness.Count(r => r.Category == SafeCategory.RecoveryFocus) / total;
        var modify = readiness.Count(r => r.Category == SafeCategory.ModifyLoad) / total;

        if (recovery >= 0.20) return "Recovery emphasis";
        if (modify >= 0.30) return "Reduced";
        return "Standard";
    }

    private static IReadOnlyList<PlanBlock> BuildBlocks(string intensity, string focus) => intensity switch
    {
        "Recovery emphasis" => new[]
        {
            new PlanBlock("warmup", "Mobility flow + breathing", 12, "Re-set the nervous system, low impact only.", intensity),
            new PlanBlock("technical", "Technique reset: " + focus, 18, "Slow, deliberate reps. No collisions.", intensity),
            new PlanBlock("game", "Walk-through small-sided game", 12, "Decision making at walking pace; emphasise communication.", intensity),
            new PlanBlock("decision", "Tactical chalk-talk", 8, "Whiteboard the previous session's review themes.", intensity),
            new PlanBlock("cooldown", "Extended cool-down + check-in", 10, "Optional voluntary mobility; coach checks in 1:1.", intensity),
        },
        "Reduced" => new[]
        {
            new PlanBlock("warmup", "RAMP warm-up", 12, "Moderate intensity, full range of movement.", intensity),
            new PlanBlock("technical", focus + " (controlled)", 20, "Skill work at 70% intensity, prioritise quality.", intensity),
            new PlanBlock("game", "Conditioned small-sided game", 18, "Constraint-led; reduce contact volume.", intensity),
            new PlanBlock("decision", "Pattern recognition drill", 10, "Coach calls; players cue.", intensity),
            new PlanBlock("cooldown", "Cool-down + review", 10, "Light jog, mobility, ask players to self-rate.", intensity),
        },
        _ => new[]
        {
            new PlanBlock("warmup", "RAMP warm-up + activation", 12, "Full intensity ramp, prep contact loads.", intensity),
            new PlanBlock("technical", focus, 22, "Skill work at game intensity.", intensity),
            new PlanBlock("game", "Open small-sided game", 22, "Live decision making, game-realistic loads.", intensity),
            new PlanBlock("decision", "Scenario play", 12, "Apply session focus under pressure.", intensity),
            new PlanBlock("cooldown", "Cool-down + review", 10, "Mobility + 60s coach summary.", intensity),
        },
    };

    private static string BuildSummary(string intensity, string focus, SessionPlanContext ctx)
    {
        var count = ctx.Readiness.Count;
        if (count == 0)
        {
            return $"Plan focus: {focus}. Intensity bucket: {intensity}. " +
                   "No readiness data yet — coach should prompt a check-in before the session.";
        }

        var ready = ctx.Readiness.Count(r => r.Category == SafeCategory.Ready);
        var monitor = ctx.Readiness.Count(r => r.Category == SafeCategory.Monitor);
        var modify = ctx.Readiness.Count(r => r.Category == SafeCategory.ModifyLoad);
        var recovery = ctx.Readiness.Count(r => r.Category == SafeCategory.RecoveryFocus);

        return $"Plan focus: {focus}. Intensity bucket: {intensity}. " +
               $"Squad readiness: {ready} Ready, {monitor} Monitor, {modify} Modify Load, {recovery} Recovery Focus. " +
               (recovery > 0
                   ? "Pair Recovery Focus players with low-impact stations."
                   : modify > 0
                       ? "Use station rotation so Modify Load players can opt out of high-impact reps."
                       : "Squad is ready for a standard load.");
    }
}
