namespace ForgeRise.Api.Data.Entities.Video;

/// <summary>
/// A short voice memo recorded by a coach against a video moment ("watch
/// the inside shoulder here"). Stored as an audio asset and optionally
/// transcribed by the worker (V6+).
/// </summary>
public sealed class CoachVoiceNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public Guid VideoAssetId { get; set; }
    public VideoAsset VideoAsset { get; set; } = null!;

    public double AtSeconds { get; set; }
    public required string StoragePath { get; set; }
    public double DurationSeconds { get; set; }

    /// <summary>Filled by the transcription worker; null until available.</summary>
    public string? TranscriptText { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}
