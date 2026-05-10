namespace ForgeRise.Api.Data.Entities.Video;

/// <summary>
/// A heuristic- or AI-proposed clip that the system thinks the coach might
/// want to keep. The coach can promote a candidate to a real
/// <see cref="VideoClip"/> or dismiss it. Score is provider-defined and
/// only used for ranking within a team.
/// </summary>
public sealed class HighlightCandidate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public Guid VideoAssetId { get; set; }
    public VideoAsset VideoAsset { get; set; } = null!;

    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public double Score { get; set; }
    public required string Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DismissedAt { get; set; }
    public Guid? PromotedToClipId { get; set; }
}
