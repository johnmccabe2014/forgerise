using System.Security.Cryptography;
using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.WelfareModule;
using ForgeRise.Api.WelfareModule.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Controllers;

/// <summary>
/// Coach-issued, single-use codes that let a user claim a roster
/// <see cref="Player"/>. Mirrors <see cref="TeamsController"/>'s invite flow:
/// any team member can issue/list/revoke; any authenticated user can redeem.
/// </summary>
[ApiController]
[Authorize]
public sealed class PlayerInvitesController : ControllerBase
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    private readonly AppDbContext _db;
    private readonly ILogger<PlayerInvitesController> _log;

    public PlayerInvitesController(AppDbContext db, ILogger<PlayerInvitesController> log)
    {
        _db = db;
        _log = log;
    }

    private static PlayerInviteDto ToDto(PlayerInvite i) =>
        new(i.Id, i.PlayerId, i.Code, i.CreatedAt, i.ExpiresAt, i.ConsumedAt, i.RevokedAt);

    [HttpGet("teams/{teamId:guid}/players/{playerId:guid}/invites")]
    public async Task<IActionResult> List(Guid teamId, Guid playerId, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var invites = await _db.PlayerInvites
            .Where(i => i.PlayerId == playerId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
        return Ok(invites.Select(ToDto));
    }

    [HttpPost("teams/{teamId:guid}/players/{playerId:guid}/invites")]
    public async Task<IActionResult> Create(Guid teamId, Guid playerId, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var userId = User.TryGetUserId()!.Value;
        var invite = new PlayerInvite
        {
            PlayerId = playerId,
            Code = GenerateInviteCode(),
            CreatedByUserId = userId,
            ExpiresAt = DateTimeOffset.UtcNow.Add(InviteLifetime),
        };
        _db.PlayerInvites.Add(invite);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("player_invite.created {PlayerId} {InviteId}", playerId, invite.Id);
        return Created(string.Empty, ToDto(invite));
    }

    [HttpDelete("teams/{teamId:guid}/players/{playerId:guid}/invites/{inviteId:guid}")]
    public async Task<IActionResult> Revoke(Guid teamId, Guid playerId, Guid inviteId, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var invite = await _db.PlayerInvites.FirstOrDefaultAsync(
            i => i.Id == inviteId && i.PlayerId == playerId, ct);
        if (invite is null) return NotFound();
        if (invite.RevokedAt is not null || invite.ConsumedAt is not null) return NoContent();

        invite.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("player_invite.revoked {PlayerId} {InviteId}", playerId, inviteId);
        return NoContent();
    }

    /// <summary>
    /// Anyone with a valid code can claim the player. Idempotent: re-running
    /// with the same code as the same user just returns the existing link
    /// without consuming the code. Distinct from /teams/join because this
    /// creates a <see cref="PlayerLink"/> rather than a coach membership.
    /// </summary>
    [HttpPost("player-invites/redeem")]
    public async Task<IActionResult> Redeem([FromBody] RedeemPlayerInviteRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        var code = request.Code.Trim();
        var invite = await _db.PlayerInvites
            .Include(i => i.Player).ThenInclude(p => p.Team)
            .FirstOrDefaultAsync(i => i.Code == code, ct);
        if (invite is null) return NotFound(new { error = "invite_not_found" });
        if (invite.Player.DeletedAt is not null || invite.Player.Team.DeletedAt is not null)
            return NotFound(new { error = "invite_not_found" });

        // Idempotency for the same user: if they already hold a link for this
        // player (typically because they consumed the same code earlier),
        // return the existing claim instead of re-validating the invite state.
        var existing = await _db.PlayerLinks
            .FirstOrDefaultAsync(l => l.PlayerId == invite.PlayerId && l.UserId == userId, ct);
        if (existing is not null)
        {
            return Ok(new RedeemPlayerInviteResponse(
                invite.PlayerId, invite.Player.TeamId,
                invite.Player.DisplayName, invite.Player.Team.Name));
        }

        if (invite.RevokedAt is not null) return Conflict(new { error = "invite_revoked" });
        if (invite.ConsumedAt is not null) return Conflict(new { error = "invite_consumed" });
        if (invite.ExpiresAt <= DateTimeOffset.UtcNow) return Conflict(new { error = "invite_expired" });

        _db.PlayerLinks.Add(new PlayerLink
        {
            PlayerId = invite.PlayerId,
            UserId = userId.Value,
        });
        invite.ConsumedAt = DateTimeOffset.UtcNow;
        invite.ConsumedByUserId = userId.Value;
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("player_invite.redeemed {PlayerId} {UserId} {InviteId}",
            invite.PlayerId, userId, invite.Id);

        return Ok(new RedeemPlayerInviteResponse(
            invite.PlayerId, invite.Player.TeamId,
            invite.Player.DisplayName, invite.Player.Team.Name));
    }

    private static string GenerateInviteCode()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");
    }
}
