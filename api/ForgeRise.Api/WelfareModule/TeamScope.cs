using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.WelfareModule;

/// <summary>
/// Centralised authorization probe for team-scoped controllers. Membership-aware:
/// "owned" semantics now mean "the caller is a member of the team in any role".
/// Owner-only operations (delete team, manage coaches/invites) call
/// <see cref="RequireTeamOwner"/>.
///
/// Returns 401 if the caller has no user id, 404 if the team/player does not
/// exist, 403 if the caller is authenticated but not a member (or not the
/// owner for owner-only endpoints).
/// </summary>
internal static class TeamScope
{
    public static async Task<(Team? team, IActionResult? error)> RequireOwnedTeam(
        ControllerBase controller, AppDbContext db, Guid teamId, CancellationToken ct)
    {
        var userId = controller.User.TryGetUserId();
        if (userId is null) return (null, controller.Unauthorized());

        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return (null, controller.NotFound());

        var isMember = await db.TeamMemberships
            .AnyAsync(m => m.TeamId == teamId && m.UserId == userId, ct);
        if (!isMember) return (null, controller.Forbid());
        return (team, null);
    }

    public static async Task<(Team? team, IActionResult? error)> RequireTeamOwner(
        ControllerBase controller, AppDbContext db, Guid teamId, CancellationToken ct)
    {
        var userId = controller.User.TryGetUserId();
        if (userId is null) return (null, controller.Unauthorized());

        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return (null, controller.NotFound());

        var isOwner = await db.TeamMemberships
            .AnyAsync(m => m.TeamId == teamId && m.UserId == userId && m.Role == TeamRole.Owner, ct);
        if (!isOwner) return (null, controller.Forbid());
        return (team, null);
    }

    public static async Task<(Team? team, Player? player, IActionResult? error)> RequireOwnedPlayer(
        ControllerBase controller, AppDbContext db, Guid teamId, Guid playerId, CancellationToken ct)
    {
        var (team, err) = await RequireOwnedTeam(controller, db, teamId, ct);
        if (err is not null) return (null, null, err);

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId && p.TeamId == teamId, ct);
        if (player is null) return (team, null, controller.NotFound());
        return (team, player, null);
    }
}
