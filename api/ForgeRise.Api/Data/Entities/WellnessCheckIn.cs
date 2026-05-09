using ForgeRise.Api.Welfare;

namespace ForgeRise.Api.Data.Entities;

/// <summary>
/// Wellness check-in for a single player. Raw scores are server-side only;
/// only <see cref="Category"/> is ever surfaced to coach UIs.
/// </summary>
public class WellnessCheckIn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>The user (coach or self) who recorded the check-in.</summary>
    public Guid RecordedByUserId { get; set; }

    /// <summary>
    /// True when the check-in was submitted by the player themselves via the
    /// self-service endpoint, false when a coach recorded it on their behalf.
    /// Used for audit/provenance — does not change category derivation.
    /// </summary>
    public bool SubmittedBySelf { get; set; }

    public DateTimeOffset AsOf { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // --- Raw welfare fields. NEVER expose directly to coach views. ---
    public double? SleepHours { get; set; }
    public int? SorenessScore { get; set; }
    public int? MoodScore { get; set; }
    public int? StressScore { get; set; }
    public int? FatigueScore { get; set; }
    public string? InjuryNotes { get; set; }

    // --- Snapshot of the derived category at record time. Survives raw purge. ---
    public SafeCategory Category { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset? RawPurgedAt { get; set; }
}
