namespace ForgeRise.Api.Data.Entities.Video;

/// <summary>
/// Many-to-many bridge: a coach can attach the same <see cref="VideoAsset"/>
/// to one or more <see cref="Session"/>s (warm-up clip reused across the
/// week, etc.). <c>TeamId</c> is denormalised onto the row so every query
/// can filter on it without a join.
/// </summary>
public sealed class SessionVideoLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public Guid SessionId { get; set; }
    public Session Session { get; set; } = null!;

    public Guid VideoAssetId { get; set; }
    public VideoAsset VideoAsset { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
