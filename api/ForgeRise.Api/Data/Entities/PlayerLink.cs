namespace ForgeRise.Api.Data.Entities;

/// <summary>
/// Links an authenticated <see cref="User"/> to a roster <see cref="Player"/>
/// so the player can self-serve (own profile, own check-ins, own incidents).
///
/// v1 keeps this minimal: the linked user IS the player. Guardian/minor flows
/// are deliberately deferred — see TODO for a Role enum + GuardianEmail when
/// we tackle the under-16 consent gate.
/// </summary>
public sealed class PlayerLink
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset ClaimedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
