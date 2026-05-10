namespace ForgeRise.Api.Data.Entities.Video;

/// <summary>
/// One segment of an automatic transcript over a <see cref="VideoAsset"/>'s
/// audio track. Produced by the transcription worker; never edited by users.
/// </summary>
public sealed class TranscriptSegment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public Guid VideoAssetId { get; set; }
    public VideoAsset VideoAsset { get; set; } = null!;

    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public required string Text { get; set; }
    public double? Confidence { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
