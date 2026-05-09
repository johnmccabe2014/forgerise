namespace ForgeRise.Api.Data.Entities;

public enum SessionType
{
    Training = 0,
    Match = 1,
    Other = 2,
}

/// <summary>
/// A scheduled or completed coaching session. The optional review fields
/// (<see cref="ReviewNotes"/> + <see cref="ReviewedAt"/>) capture the coach's
/// short post-session reflection that the AI session-plan generator reads.
/// Free-form notes are coach-only; they are not welfare data and are not
/// covered by the redaction policy, but they are still scoped to the team.
/// </summary>
public sealed class Session
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public SessionType Type { get; set; }
    public string? Location { get; set; }
    public string? Focus { get; set; }

    public string? ReviewNotes { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// If this session was created by adopting a <see cref="SessionPlan"/>,
    /// the originating plan id is captured here so the UI can link both
    /// directions and surface "from plan" badges.
    /// </summary>
    public Guid? SourceSessionPlanId { get; set; }
}
