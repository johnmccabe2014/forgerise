using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.WelfareModule;

/// <summary>
/// Centralised ownership probe for team-scoped controllers. Returns 401 if the
/// caller has no user id, 404 if the team or player does not belong to that user,
/// 403 if the caller is authenticated but not the team owner.
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
        if (team.OwnerUserId != userId) return (null, controller.Forbid());
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
