namespace ForgeRise.Api.Data.Entities;

/// <summary>
/// Per-coach watermark on a team's activity feed.
///
/// One row per (TeamId, UserId). LastSeenAt is bumped to "now" whenever the
/// coach pulls up the team page so the next visit can compute "what's new
/// since you last looked". Pure UX state — never feeds the welfare audit log.
/// </summary>
public sealed class TeamActivitySeen
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }

    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}
