namespace ForgeRise.Api.Data.Entities;

/// <summary>
/// Per-team override for a drill in <c>DrillCatalogue</c>. Coaches can mark a
/// drill as a favourite (boost in the recommender) or exclude it entirely.
/// One row per (TeamId, DrillId). Pure planning preference — never feeds the
/// welfare audit log.
/// </summary>
public enum DrillPreferenceStatus
{
    Favourite = 0,
    Exclude = 1,
}

public sealed class TeamDrillPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TeamId { get; set; }
    public string DrillId { get; set; } = string.Empty;

    public DrillPreferenceStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
