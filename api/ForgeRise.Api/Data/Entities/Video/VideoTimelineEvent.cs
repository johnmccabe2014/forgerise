namespace ForgeRise.Api.Data.Entities.Video;

/// <summary>
/// A point-in-time annotation on a video timeline. Created by a coach
/// scrubbing the player. <see cref="Kind"/> drives both UX badge colour
/// and the welfare-scrub policy.
/// </summary>
public sealed class VideoTimelineEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public Guid VideoAssetId { get; set; }
    public VideoAsset VideoAsset { get; set; } = null!;

    public double AtSeconds { get; set; }
    public VideoTimelineEventKind Kind { get; set; } = VideoTimelineEventKind.Note;

    /// <summary>
    /// Free-text body. For <see cref="VideoTimelineEventKind.WelfareFlag"/>
    /// rows, the read/list endpoint MUST scrub this for non-welfare viewers
    /// (master prompt §9). V1 has no read endpoint, so no scrub is required
    /// yet.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}
