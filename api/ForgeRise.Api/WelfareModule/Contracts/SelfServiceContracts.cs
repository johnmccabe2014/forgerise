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
    DateTimeOffset? RevokedAt);

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
