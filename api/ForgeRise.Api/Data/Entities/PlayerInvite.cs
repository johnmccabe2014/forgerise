namespace ForgeRise.Api.Data.Entities;

/// <summary>
/// Single-use invite code that lets a user claim a specific
/// <see cref="Player"/> on the parent team. Mirrors <see cref="TeamInvite"/>:
/// owner/coach generates a code, shares it out-of-band, code expires after 7
/// days, can be revoked, and is consumed on first redemption.
/// </summary>
public sealed class PlayerInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    /// <summary>Random URL-safe code, globally unique.</summary>
    public required string Code { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
    public Guid? ConsumedByUserId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
