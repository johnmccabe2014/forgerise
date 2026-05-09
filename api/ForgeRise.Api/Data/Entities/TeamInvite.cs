namespace ForgeRise.Api.Data.Entities;

/// <summary>
/// Shareable single-use invite code that promotes the redeeming user to Coach
/// on the parent team. v1 deliberately avoids email infra: the owner generates
/// a code and shares it out-of-band. Codes auto-expire (default 7 days),
/// cannot be reused, and can be revoked by the owner.
/// </summary>
public sealed class TeamInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    /// <summary>Random URL-safe code, globally unique.</summary>
    public required string Code { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
    public Guid? ConsumedByUserId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
