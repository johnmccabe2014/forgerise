using System.ComponentModel.DataAnnotations;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Welfare;

namespace ForgeRise.Api.Sessions.Contracts;

public sealed class CreateSessionRequest
{
    [Required] public DateTimeOffset ScheduledAt { get; init; }
    [Range(5, 480)] public int DurationMinutes { get; init; } = 60;
    [Required] public SessionType Type { get; init; }
    [StringLength(120)] public string? Location { get; init; }
    [StringLength(200)] public string? Focus { get; init; }
}

public sealed class UpdateSessionRequest
{
    [Required] public DateTimeOffset ScheduledAt { get; init; }
    [Range(5, 480)] public int DurationMinutes { get; init; } = 60;
    [Required] public SessionType Type { get; init; }
    [StringLength(120)] public string? Location { get; init; }
    [StringLength(200)] public string? Focus { get; init; }
}

public sealed class ReviewSessionRequest
{
    [Required, StringLength(4_000, MinimumLength = 1)]
    public string ReviewNotes { get; init; } = string.Empty;
}

public sealed record SessionDto(
    Guid Id, Guid TeamId, DateTimeOffset ScheduledAt, int DurationMinutes,
    SessionType Type, string? Location, string? Focus,
    string? ReviewNotes, DateTimeOffset? ReviewedAt, DateTimeOffset CreatedAt,
    Guid? SourceSessionPlanId = null);

public sealed class AttendanceUpsertItem
{
    [Required] public Guid PlayerId { get; init; }
    [Required] public AttendanceStatus Status { get; init; }
    [StringLength(500)] public string? Note { get; init; }
}

public sealed class AttendanceBulkUpsertRequest
{
    [Required, MinLength(1)]
    public IReadOnlyList<AttendanceUpsertItem> Items { get; init; } = Array.Empty<AttendanceUpsertItem>();
}

public sealed record AttendanceRowDto(Guid PlayerId, string PlayerDisplayName, AttendanceStatus Status, string? Note, DateTimeOffset? RecordedAt);

public sealed class GenerateSessionPlanRequest
{
    [StringLength(200)] public string? Focus { get; init; }
    public Guid? BasedOnSessionId { get; init; }
}

public sealed record SessionPlanReadinessRow(Guid PlayerId, SafeCategory Category);

/// <summary>
/// A plain-English explanation of jargon ("RAMP", "SSG", "ruck", ...) attached
/// to a plan block or drill so a brand-new coach is never staring at shorthand
/// they don't recognise. Always optional on the wire — old persisted plans
/// won't have it and the client must tolerate an empty list.
/// </summary>
public sealed record GlossaryTermDto(string Term, string Plain);

public sealed record SessionPlanBlockDto(
    string Block,
    string Title,
    int DurationMinutes,
    string Intent,
    string Intensity,
    IReadOnlyList<GlossaryTermDto>? Glossary = null);

public sealed record SessionPlanRecommendationDto(
    string DrillId,
    string Title,
    string Description,
    int DurationMinutes,
    string Rationale,
    IReadOnlyList<string> Tags,
    // Enriched fields are computed on-read from the static catalogue, never
    // persisted in RecommendationsJson — that way swapping coaching content
    // is instant and old plans keep working.
    string? LongDescription = null,
    IReadOnlyList<string>? CoachingCues = null,
    IReadOnlyList<string>? Equipment = null,
    string? WhatItMeans = null,
    string? DiagramKey = null,
    IReadOnlyList<GlossaryTermDto>? Glossary = null);

/// <summary>
/// Public drill-catalogue DTO returned by GET /drills and GET /drills/{id}.
/// Used by the run-session UI to render the coaching card and diagram for
/// each drill the coach is about to take the team through.
/// </summary>
public sealed record DrillDto(
    string Id,
    string Title,
    string Description,
    int DurationMinutes,
    IReadOnlyList<string> Tags,
    string LongDescription,
    IReadOnlyList<string> CoachingCues,
    IReadOnlyList<string> Equipment,
    string WhatItMeans,
    string DiagramKey,
    IReadOnlyList<GlossaryTermDto> Glossary);

public sealed record SessionPlanDto(
    Guid Id,
    Guid TeamId,
    DateTimeOffset GeneratedAt,
    Guid? BasedOnSessionId,
    string Focus,
    string Summary,
    IReadOnlyList<SessionPlanBlockDto> Blocks,
    IReadOnlyList<SessionPlanReadinessRow> ReadinessSnapshot,
    IReadOnlyList<SessionPlanRecommendationDto> Recommendations,
    int RecentSelfIncidentCount = 0,
    DateTimeOffset? AdoptedAt = null,
    Guid? AdoptedSessionId = null,
    DateTimeOffset? PinnedAt = null,
    DateTimeOffset? ArchivedAt = null);

public sealed class AdoptSessionPlanRequest
{
    [Required] public DateTimeOffset ScheduledAt { get; init; }
    [Range(5, 480)] public int DurationMinutes { get; init; } = 75;
    [Required] public SessionType Type { get; init; } = SessionType.Training;
    [StringLength(120)] public string? Location { get; init; }
}
