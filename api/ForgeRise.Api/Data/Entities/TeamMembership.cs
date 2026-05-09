namespace ForgeRise.Api.Data.Entities;

/// <summary>
/// Role of a user on a team. Owner has full control (delete, manage coaches);
/// Coach can read + write players/sessions/welfare but cannot delete the team
/// or remove other coaches. v1 only — keep narrow.
/// </summary>
public enum TeamRole
{
    Owner = 1,
    Coach = 2,
}

/// <summary>
/// Join row between <see cref="User"/> and <see cref="Team"/>. Every team has
/// at least one Owner row (created at team-creation time and backfilled by
/// migration for legacy teams). Coaches are added by accepting a
/// <see cref="TeamInvite"/> code.
/// </summary>
public sealed class TeamMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public TeamRole Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}
