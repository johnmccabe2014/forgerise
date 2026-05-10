namespace ForgeRise.Api.Data.Entities.Video;

/// <summary>
/// A processed (or in-flight) video belonging to a team. The
/// <see cref="StoragePath"/> is opaque to the API — only resolved through
/// <c>IObjectStore</c>. MIME type is the post-probe value, not the
/// client-declared one (see security review iter1 standing rule #3).
/// </summary>
public sealed class VideoAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }
    public required string OriginalFileName { get; set; }
    public required string MimeType { get; set; }
    public long SizeBytes { get; set; }
    public double? DurationSeconds { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    public required string StoragePath { get; set; }
    public string? ThumbnailPath { get; set; }

    public VideoProcessingState ProcessingState { get; set; } = VideoProcessingState.Queued;
    public string? ProcessingError { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
