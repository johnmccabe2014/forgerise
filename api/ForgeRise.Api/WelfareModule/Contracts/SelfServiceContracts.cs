using System.ComponentModel.DataAnnotations;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Welfare;

namespace ForgeRise.Api.WelfareModule.Contracts;

// ---------- Player invite (coach-issued, redeemed by player) ----------

public sealed record PlayerInviteDto(
    Guid Id,
    Guid PlayerId,
    string Code,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt,
    DateTimeOffset? RevokedAt,
    bool RequiresGuardianConsent,
    bool GuardianConsentAcknowledged);

public sealed class CreatePlayerInviteRequest
{
    /// <summary>
    /// Coach attestation that they have guardian consent for an under-16
    /// player. Required when the target player is under 16; ignored otherwise.
    /// </summary>
    public bool GuardianConsentAcknowledged { get; init; }
}

public sealed class RedeemPlayerInviteRequest
{
    [Required, StringLength(64, MinimumLength = 1)]
    public string Code { get; init; } = string.Empty;
}

public sealed record RedeemPlayerInviteResponse(
    Guid PlayerId,
    Guid TeamId,
    string PlayerDisplayName,
    string TeamName);

// ---------- /me self-service ----------

/// <summary>One linked player as seen by the player themselves.</summary>
public sealed record MyLinkedPlayerDto(
    Guid PlayerId,
    string PlayerDisplayName,
    Guid TeamId,
    string TeamName,
    DateTimeOffset ClaimedAt);

/// <summary>
/// Self-service view of one of the player's own check-ins. Players may see
/// their own raw fields — they own the data — but the shape mirrors the
/// coach raw view so the audit log entry remains consistent.
/// </summary>
public sealed record MyCheckInDto(
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
    string CategoryLabel,
    bool SubmittedBySelf);

/// <summary>
/// Self-service view of one of the player's own incidents. Player owns the
/// data, so raw notes are returned. Severity is capped at Medium for
/// player-filed reports — anything more serious requires coach review.
/// </summary>
public sealed record MyIncidentDto(
    Guid Id,
    Guid PlayerId,
    DateTimeOffset OccurredAt,
    IncidentSeverity Severity,
    string Summary,
    string? Notes,
    bool SubmittedBySelf,
    DateTimeOffset? AcknowledgedAt = null,
    string? AcknowledgedByDisplayName = null);

public sealed class CreateSelfIncidentRequest
{
    /// <summary>Low or Medium only — High is rejected for self-reports.</summary>
    [Required] public IncidentSeverity Severity { get; init; }

    [Required, StringLength(280, MinimumLength = 1)]
    public string Summary { get; init; } = string.Empty;

    [StringLength(4_000)] public string? Notes { get; init; }
    public DateTimeOffset? OccurredAt { get; init; }
}
