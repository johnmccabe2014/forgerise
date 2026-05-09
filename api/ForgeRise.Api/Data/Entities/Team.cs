namespace ForgeRise.Api.Data.Entities;

public sealed class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public User Owner { get; set; } = null!;

    public required string Name { get; set; }
    public required string Code { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<TeamMembership> Memberships { get; set; } = new List<TeamMembership>();
    public ICollection<TeamInvite> Invites { get; set; } = new List<TeamInvite>();
}
