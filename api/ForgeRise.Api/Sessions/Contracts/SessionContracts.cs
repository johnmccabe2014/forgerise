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
    string? ReviewNotes, DateTimeOffset? ReviewedAt, DateTimeOffset CreatedAt);

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

public sealed record SessionPlanBlockDto(string Block, string Title, int DurationMinutes, string Intent, string Intensity);

public sealed record SessionPlanRecommendationDto(
    string DrillId,
    string Title,
    string Description,
    int DurationMinutes,
    string Rationale,
    IReadOnlyList<string> Tags);

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
    int RecentSelfIncidentCount = 0);
