using System.ComponentModel.DataAnnotations;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Welfare;

namespace ForgeRise.Api.WelfareModule.Contracts;

public sealed class CreateCheckInRequest
{
    [Range(0, 24)] public double? SleepHours { get; init; }
    [Range(1, 5)] public int? SorenessScore { get; init; }
    [Range(1, 5)] public int? MoodScore { get; init; }
    [Range(1, 5)] public int? StressScore { get; init; }
    [Range(1, 5)] public int? FatigueScore { get; init; }
    [StringLength(2_000)] public string? InjuryNotes { get; init; }
    public DateTimeOffset? AsOf { get; init; }
}

/// <summary>Coach-safe view of a check-in. NEVER includes raw scores or notes.</summary>
public sealed record CheckInSummaryDto(Guid Id, Guid PlayerId, DateTimeOffset AsOf, SafeCategory Category, string CategoryLabel, bool SubmittedBySelf);

/// <summary>Owner-only raw view, returned only by the audited /raw endpoint.</summary>
public sealed record CheckInRawDto(
    Guid Id,
    Guid PlayerId,
    DateTimeOffset AsOf,
    double? SleepHours,
    int? SorenessScore,
    int? MoodScore,
    int? StressScore,
    int? FatigueScore,
    string? InjuryNotes,
    SafeCategory Category,
    DateTimeOffset? RawPurgedAt);

public sealed record TeamReadinessDto(Guid PlayerId, string PlayerDisplayName, SafeCategory Category, string CategoryLabel, DateTimeOffset AsOf, bool SubmittedBySelf);

public sealed class CreateIncidentRequest
{
    [Required] public IncidentSeverity Severity { get; init; }

    [Required, StringLength(280, MinimumLength = 1)]
    public string Summary { get; init; } = string.Empty;

    [StringLength(4_000)] public string? Notes { get; init; }
    public DateTimeOffset? OccurredAt { get; init; }
}

public sealed record IncidentSummaryDto(Guid Id, Guid PlayerId, DateTimeOffset OccurredAt, IncidentSeverity Severity, string Summary, bool SubmittedBySelf, DateTimeOffset? AcknowledgedAt);

public sealed record IncidentRawDto(
    Guid Id,
    Guid PlayerId,
    DateTimeOffset OccurredAt,
    IncidentSeverity Severity,
    string Summary,
    string? Notes,
    DateTimeOffset? RawPurgedAt);

public sealed record AuditEntryDto(Guid Id, Guid ActorUserId, Guid PlayerId, Guid? SubjectId, WelfareAuditAction Action, DateTimeOffset At);
