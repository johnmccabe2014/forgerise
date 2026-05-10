namespace ForgeRise.Api.Data.Entities.Video;

/// <summary>
/// A coach-defined sub-range of a <see cref="VideoAsset"/> (e.g. the 12s of
/// a great line break). <see cref="StoragePath"/> is null until the worker
/// renders the clip out (V8); until then, the clip is virtual: a pair of
/// timestamps over the source asset.
/// </summary>
public sealed class VideoClip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public Guid VideoAssetId { get; set; }
    public VideoAsset VideoAsset { get; set; } = null!;

    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public required string Title { get; set; }

    public string? StoragePath { get; set; }
    public string? ThumbnailPath { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}
