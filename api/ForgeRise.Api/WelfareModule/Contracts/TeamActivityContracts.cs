using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Welfare;

namespace ForgeRise.Api.WelfareModule.Contracts;

/// <summary>
/// One row in the team activity feed. A simple discriminated DTO covering
/// the three player-driven events coaches want to react to: self-submitted
/// check-ins, self-reported incidents, and invite redemptions. Only fields
/// relevant to the <see cref="Kind"/> are populated.
/// </summary>
public sealed record TeamActivityEventDto(
    string Kind,
    DateTimeOffset At,
    Guid PlayerId,
    string PlayerDisplayName,
    Guid? SubjectId,
    SafeCategory? Category,
    string? CategoryLabel,
    IncidentSeverity? Severity,
    string? Summary,
    bool? Acknowledged);

public static class TeamActivityKinds
{
    public const string CheckInSelfSubmitted = "checkin_self_submitted";
    public const string IncidentSelfReported = "incident_self_reported";
    public const string InviteRedeemed = "invite_redeemed";
}

/// <summary>
/// Per-coach unread state for the team activity feed.
/// <see cref="UnreadCount"/> is capped at 99; UI shows "99+" beyond that.
/// </summary>
public sealed record TeamActivitySeenDto(
    DateTimeOffset? LastSeenAt,
    int UnreadCount);
