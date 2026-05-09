using System.Security.Cryptography;
using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Teams.Contracts;
using ForgeRise.Api.WelfareModule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Controllers;

[ApiController]
[Route("teams")]
[Authorize]
public sealed class TeamsController : ControllerBase
{
    // Invite codes live for one week. Long enough for a coach to share via DM,
    // short enough that a leaked code doesn't grant indefinite access.
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    private readonly AppDbContext _db;
    private readonly ILogger<TeamsController> _log;

    public TeamsController(AppDbContext db, ILogger<TeamsController> log)
    {
        _db = db;
        _log = log;
    }

    private static string RoleString(TeamRole role) => role switch
    {
        TeamRole.Owner => "owner",
        TeamRole.Coach => "coach",
        _ => "coach",
    };

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        // Project against memberships so coaches see teams they've joined,
        // not just teams they own.
        var rows = await _db.TeamMemberships
            .Where(m => m.UserId == userId && m.Team.DeletedAt == null)
            .OrderBy(m => m.Team.Name)
            .Select(m => new
            {
                m.Team.Id,
                m.Team.Name,
                m.Team.Code,
                m.Team.CreatedAt,
                MyRole = m.Role,
                PlayerCount = m.Team.Players.Count(p => p.DeletedAt == null),
                CoachCount = _db.TeamMemberships.Count(x => x.TeamId == m.TeamId),
            })
            .ToListAsync(ct);

        var teams = rows.Select(r => new TeamDto(r.Id, r.Name, r.Code, r.CreatedAt, r.PlayerCount)
        {
            MyRole = RoleString(r.MyRole),
            CoachCount = r.CoachCount,
        });
        return Ok(teams);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        var (team, err) = await TeamScope.RequireOwnedTeam(this, _db, id, ct);
        if (err is not null) return err;

        var role = await _db.TeamMemberships
            .Where(m => m.TeamId == id && m.UserId == userId)
            .Select(m => (TeamRole?)m.Role)
            .FirstOrDefaultAsync(ct) ?? TeamRole.Coach;
        var playerCount = await _db.Players.CountAsync(p => p.TeamId == id, ct);
        var coachCount = await _db.TeamMemberships.CountAsync(m => m.TeamId == id, ct);

        return Ok(new TeamDto(team!.Id, team.Name, team.Code, team.CreatedAt, playerCount)
        {
            MyRole = RoleString(role),
            CoachCount = coachCount,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        var code = request.Code.Trim();
        // Code uniqueness is per primary owner — matches the existing
        // (OwnerUserId, Code) unique index. Codes can collide freely across
        // different owners.
        var clash = await _db.Teams
            .IgnoreQueryFilters()
            .AnyAsync(t => t.OwnerUserId == userId && t.Code == code && t.DeletedAt == null, ct);
        if (clash) return Conflict(new { error = "team_code_conflict" });

        var team = new Team
        {
            OwnerUserId = userId.Value,
            Name = request.Name.Trim(),
            Code = code,
        };
        _db.Teams.Add(team);
        // The creating coach is automatically the first Owner membership.
        _db.TeamMemberships.Add(new TeamMembership
        {
            TeamId = team.Id,
            UserId = userId.Value,
            Role = TeamRole.Owner,
        });
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("teams.created {TeamId} {OwnerUserId}", team.Id, userId);
        return CreatedAtAction(nameof(Get), new { id = team.Id },
            new TeamDto(team.Id, team.Name, team.Code, team.CreatedAt, 0)
            {
                MyRole = "owner",
                CoachCount = 1,
            });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTeamRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // Renaming is low-risk; allow any member.
        var (team, err) = await TeamScope.RequireOwnedTeam(this, _db, id, ct);
        if (err is not null) return err;

        team!.Name = request.Name.Trim();
        await _db.SaveChangesAsync(ct);

        var userId = User.TryGetUserId()!.Value;
        var role = await _db.TeamMemberships
            .Where(m => m.TeamId == id && m.UserId == userId)
            .Select(m => (TeamRole?)m.Role)
            .FirstOrDefaultAsync(ct) ?? TeamRole.Coach;
        var count = await _db.Players.CountAsync(p => p.TeamId == id, ct);
        var coachCount = await _db.TeamMemberships.CountAsync(m => m.TeamId == id, ct);
        return Ok(new TeamDto(team.Id, team.Name, team.Code, team.CreatedAt, count)
        {
            MyRole = RoleString(role),
            CoachCount = coachCount,
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        // Destructive: Owner only.
        var (team, err) = await TeamScope.RequireTeamOwner(this, _db, id, ct);
        if (err is not null) return err;

        team!.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("teams.deleted {TeamId}", id);
        return NoContent();
    }

    // ---------------- Coach roster ----------------

    [HttpGet("{id:guid}/coaches")]
    public async Task<IActionResult> ListCoaches(Guid id, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, id, ct);
        if (err is not null) return err;

        var coaches = await _db.TeamMemberships
            .Where(m => m.TeamId == id)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.JoinedAt)
            .Select(m => new TeamCoachDto(
                m.UserId,
                m.User.DisplayName,
                m.User.Email,
                m.Role == TeamRole.Owner ? "owner" : "coach",
                m.JoinedAt))
            .ToListAsync(ct);

        return Ok(coaches);
    }

    [HttpDelete("{id:guid}/coaches/{coachUserId:guid}")]
    public async Task<IActionResult> RemoveCoach(Guid id, Guid coachUserId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireTeamOwner(this, _db, id, ct);
        if (err is not null) return err;

        var membership = await _db.TeamMemberships
            .FirstOrDefaultAsync(m => m.TeamId == id && m.UserId == coachUserId, ct);
        if (membership is null) return NotFound();

        // Refuse to remove the last owner — would orphan the team. Caller
        // must transfer ownership first (out of scope for v1).
        if (membership.Role == TeamRole.Owner)
        {
            var ownerCount = await _db.TeamMemberships
                .CountAsync(m => m.TeamId == id && m.Role == TeamRole.Owner, ct);
            if (ownerCount <= 1)
                return Conflict(new { error = "cannot_remove_last_owner" });
        }

        _db.TeamMemberships.Remove(membership);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("teams.coach_removed {TeamId} {UserId}", id, coachUserId);
        return NoContent();
    }

    /// <summary>
    /// Transfer ownership of a team from the calling Owner to another existing
    /// coach. The caller is demoted to Coach and the target is promoted to
    /// Owner in a single transaction so the team is never ownerless and never
    /// has zero members. Idempotent: transferring to yourself, or to someone
    /// who is already the sole Owner with the caller as Coach, is a no-op.
    /// </summary>
    [HttpPost("{id:guid}/coaches/{coachUserId:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(Guid id, Guid coachUserId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireTeamOwner(this, _db, id, ct);
        if (err is not null) return err;

        var callerId = User.TryGetUserId()!.Value;

        // Self-transfer is a no-op rather than an error so the UI can be naive.
        if (callerId == coachUserId) return NoContent();

        var target = await _db.TeamMemberships
            .FirstOrDefaultAsync(m => m.TeamId == id && m.UserId == coachUserId, ct);
        if (target is null) return NotFound(new { error = "target_not_a_coach" });

        var caller = await _db.TeamMemberships
            .FirstAsync(m => m.TeamId == id && m.UserId == callerId, ct);

        // Both updates land in one SaveChanges call, which EF wraps in a
        // single transaction — so we never observe a state with zero Owners.
        target.Role = TeamRole.Owner;
        caller.Role = TeamRole.Coach;
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "teams.ownership_transferred {TeamId} {FromUserId} {ToUserId}",
            id, callerId, coachUserId);
        return NoContent();
    }

    // ---------------- Invites ----------------

    [HttpGet("{id:guid}/invites")]
    public async Task<IActionResult> ListInvites(Guid id, CancellationToken ct)
    {
        // Codes are effectively credentials — owner-only.
        var (_, err) = await TeamScope.RequireTeamOwner(this, _db, id, ct);
        if (err is not null) return err;

        var invites = await _db.TeamInvites
            .Where(i => i.TeamId == id)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new TeamInviteDto(i.Id, i.Code, i.CreatedAt, i.ExpiresAt, i.ConsumedAt, i.RevokedAt))
            .ToListAsync(ct);

        return Ok(invites);
    }

    [HttpPost("{id:guid}/invites")]
    public async Task<IActionResult> CreateInvite(Guid id, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireTeamOwner(this, _db, id, ct);
        if (err is not null) return err;

        var userId = User.TryGetUserId()!.Value;
        var invite = new TeamInvite
        {
            TeamId = id,
            Code = GenerateInviteCode(),
            CreatedByUserId = userId,
            ExpiresAt = DateTimeOffset.UtcNow.Add(InviteLifetime),
        };
        _db.TeamInvites.Add(invite);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("teams.invite_created {TeamId} {InviteId}", id, invite.Id);
        return Created(string.Empty, new TeamInviteDto(
            invite.Id, invite.Code, invite.CreatedAt, invite.ExpiresAt, null, null));
    }

    [HttpDelete("{id:guid}/invites/{inviteId:guid}")]
    public async Task<IActionResult> RevokeInvite(Guid id, Guid inviteId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireTeamOwner(this, _db, id, ct);
        if (err is not null) return err;

        var invite = await _db.TeamInvites.FirstOrDefaultAsync(i => i.Id == inviteId && i.TeamId == id, ct);
        if (invite is null) return NotFound();
        if (invite.RevokedAt is not null || invite.ConsumedAt is not null) return NoContent();

        invite.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------------- Join ----------------

    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinTeamRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        var code = request.Code.Trim();
        var invite = await _db.TeamInvites
            .Include(i => i.Team)
            .FirstOrDefaultAsync(i => i.Code == code, ct);
        if (invite is null) return NotFound(new { error = "invite_not_found" });
        if (invite.RevokedAt is not null) return Conflict(new { error = "invite_revoked" });
        if (invite.ConsumedAt is not null) return Conflict(new { error = "invite_consumed" });
        if (invite.ExpiresAt <= DateTimeOffset.UtcNow) return Conflict(new { error = "invite_expired" });
        if (invite.Team.DeletedAt is not null) return NotFound(new { error = "invite_not_found" });

        // Idempotent: if the user is already on the team, just return the team
        // and DON'T consume the invite — keep it usable for the next coach.
        var existing = await _db.TeamMemberships
            .FirstOrDefaultAsync(m => m.TeamId == invite.TeamId && m.UserId == userId, ct);
        if (existing is null)
        {
            _db.TeamMemberships.Add(new TeamMembership
            {
                TeamId = invite.TeamId,
                UserId = userId.Value,
                Role = TeamRole.Coach,
            });
            invite.ConsumedAt = DateTimeOffset.UtcNow;
            invite.ConsumedByUserId = userId.Value;
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("teams.joined {TeamId} {UserId}", invite.TeamId, userId);
        }

        var team = invite.Team;
        var playerCount = await _db.Players.CountAsync(p => p.TeamId == team.Id, ct);
        var coachCount = await _db.TeamMemberships.CountAsync(m => m.TeamId == team.Id, ct);
        var role = existing?.Role ?? TeamRole.Coach;
        return Ok(new TeamDto(team.Id, team.Name, team.Code, team.CreatedAt, playerCount)
        {
            MyRole = RoleString(role),
            CoachCount = coachCount,
        });
    }

    /// <summary>
    /// 16 random bytes → ~22-char URL-safe base64 string. Cryptographically
    /// random; each code carries ~128 bits of entropy. '+', '/', '=' are
    /// stripped so the code can be pasted into URLs/chat without escaping.
    /// </summary>
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
