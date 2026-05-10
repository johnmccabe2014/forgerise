namespace ForgeRise.Api.Data.Entities.Video;

/// <summary>
/// Coach-defined tag (e.g. "lineout", "phase play") that can be applied to
/// assets, clips, or timeline events. Names are unique per team.
/// </summary>
public sealed class VideoTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public required string Name { get; set; }
    /// <summary>Optional CSS-friendly hex colour for the chip.</summary>
    public string? Color { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}
