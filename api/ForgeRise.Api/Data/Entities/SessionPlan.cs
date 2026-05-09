namespace ForgeRise.Api.Data.Entities;

/// <summary>
/// A coach-readable, coach-owned session plan generated from prior session
/// review + readiness snapshot. <see cref="ReadinessSnapshotJson"/> holds only
/// player ids and SafeCategory values — never raw welfare fields. Master
/// prompt §9.
/// </summary>
public sealed class SessionPlan
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public Guid GeneratedByUserId { get; set; }
    public Guid? BasedOnSessionId { get; set; }
    public string Focus { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PlanJson { get; set; } = "[]";
    public string ReadinessSnapshotJson { get; set; } = "[]";
    public string RecommendationsJson { get; set; } = "[]";

    /// <summary>
    /// Number of player self-reported incidents in the lookback window at
    /// generation time. Surfaced as the rationale for any contact reduction
    /// the recommender applied. Always &gt;= 0; default 0 keeps the column
    /// backward compatible.
    /// </summary>
    public int RecentSelfIncidentCount { get; set; }
}
