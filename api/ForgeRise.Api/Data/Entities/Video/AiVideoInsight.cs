namespace ForgeRise.Api.Data.Entities.Video;

/// <summary>
/// AI-generated insight over a video — summary, theme tags, etc. Body is a
/// structured JSON document persisted as <c>jsonb</c> in Postgres so future
/// shapes can be added without a migration. Built by <c>IInsightService</c>
/// only; raw welfare-flagged timeline events are never included in the
/// prompt that produced this row.
/// </summary>
public sealed class AiVideoInsight
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public Guid VideoAssetId { get; set; }
    public VideoAsset VideoAsset { get; set; } = null!;

    /// <summary>Discriminator: e.g. "summary", "themes", "drill-suggestions".</summary>
    public required string Kind { get; set; }
    /// <summary>JSON body. Stored as <c>jsonb</c> on Postgres.</summary>
    public string Body { get; set; } = "{}";
    /// <summary>Provider/model id, e.g. "openai:gpt-4o-mini".</summary>
    public required string Model { get; set; }
    /// <summary>Coarse cost trace — token total reported by the provider.</summary>
    public int? TokensUsed { get; set; }

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
