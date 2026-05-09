namespace ForgeRise.Api.Data.Entities;

public enum IncidentSeverity
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Coach-recorded welfare or injury incident. <see cref="Summary"/> is coach-safe;
/// <see cref="Notes"/> is raw and gated behind owner-only audited reads.
/// </summary>
public class IncidentReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    public Guid RecordedByUserId { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IncidentSeverity Severity { get; set; }

    /// <summary>Coach-safe one-liner. Returned in list endpoints.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Raw notes. Returned only by audited owner-only endpoint.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// True when the player themselves filed the incident via the
    /// self-service /me endpoint, false when a coach recorded it. Provenance
    /// metadata for audit — does not change visibility rules.
    /// </summary>
    public bool SubmittedBySelf { get; set; }

    /// <summary>
    /// Set when a coach acknowledges a self-reported incident. Coach-recorded
    /// incidents are considered acknowledged at creation; this field is
    /// primarily meaningful for triaging player-submitted reports.
    /// </summary>
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset? RawPurgedAt { get; set; }
}
