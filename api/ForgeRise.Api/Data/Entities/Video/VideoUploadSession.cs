namespace ForgeRise.Api.Data.Entities.Video;

/// <summary>
/// Tracks a multi-step upload (init -&gt; chunks -&gt; complete) so the
/// client can resume and the worker can fence partial uploads. One session
/// resolves to at most one <see cref="VideoAsset"/> on success.
/// </summary>
public sealed class VideoUploadSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }
    public required string OriginalFileName { get; set; }
    public required string DeclaredMimeType { get; set; }
    public long DeclaredSizeBytes { get; set; }

    /// <summary>Asset materialised when the upload completes successfully.</summary>
    public Guid? VideoAssetId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? AbandonedAt { get; set; }
}
