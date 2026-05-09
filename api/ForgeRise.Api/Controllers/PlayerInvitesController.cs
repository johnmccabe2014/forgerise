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
    private readonly TimeProvider _time;

    public PlayerInvitesController(AppDbContext db, ILogger<PlayerInvitesController> log, TimeProvider time)
    {
        _db = db;
        _log = log;
        _time = time;
    }

    /// <summary>
    /// True when the player's birth year implies they will turn under 16 or
    /// younger this calendar year. Conservative — we don't have day/month
    /// granularity yet, so we treat the whole birth-year cohort as minors.
    /// </summary>
    private bool IsMinor(Player player)
    {
        if (player.BirthYear is null) return false;
        var thisYear = _time.GetUtcNow().Year;
        return thisYear - player.BirthYear.Value < 16;
    }

    private PlayerInviteDto ToDto(PlayerInvite i, Player player) =>
        new(i.Id, i.PlayerId, i.Code, i.CreatedAt, i.ExpiresAt, i.ConsumedAt, i.RevokedAt,
            IsMinor(player), i.GuardianAcknowledgedAt is not null);

    [HttpGet("teams/{teamId:guid}/players/{playerId:guid}/invites")]
    public async Task<IActionResult> List(Guid teamId, Guid playerId, CancellationToken ct)
    {
        var (_, player, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var invites = await _db.PlayerInvites
            .Where(i => i.PlayerId == playerId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
        return Ok(invites.Select(i => ToDto(i, player!)));
    }

    [HttpPost("teams/{teamId:guid}/players/{playerId:guid}/invites")]
    public async Task<IActionResult> Create(
        Guid teamId, Guid playerId,
        [FromBody] CreatePlayerInviteRequest? request,
        CancellationToken ct)
    {
        var (_, player, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var userId = User.TryGetUserId()!.Value;
        var minor = IsMinor(player!);
        var ack = request?.GuardianConsentAcknowledged ?? false;
        if (minor && !ack)
        {
            return BadRequest(new { error = "guardian_consent_required" });
        }

        var invite = new PlayerInvite
        {
            PlayerId = playerId,
            Code = GenerateInviteCode(),
            CreatedByUserId = userId,
            ExpiresAt = _time.GetUtcNow().Add(InviteLifetime),
            GuardianAcknowledgedByUserId = minor ? userId : null,
            GuardianAcknowledgedAt = minor ? _time.GetUtcNow() : null,
        };
        _db.PlayerInvites.Add(invite);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "player_invite.created {PlayerId} {InviteId} minor={Minor} guardianAck={Ack}",
            playerId, invite.Id, minor, ack);
        return Created(string.Empty, ToDto(invite, player!));
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

        invite.RevokedAt = _time.GetUtcNow();
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
        if (invite.ExpiresAt <= _time.GetUtcNow()) return Conflict(new { error = "invite_expired" });

        _db.PlayerLinks.Add(new PlayerLink
        {
            PlayerId = invite.PlayerId,
            UserId = userId.Value,
        });
        invite.ConsumedAt = _time.GetUtcNow();
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
