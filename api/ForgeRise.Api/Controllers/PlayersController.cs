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
[Route("teams/{teamId:guid}/players")]
[Authorize]
public sealed class PlayersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<PlayersController> _log;

    public PlayersController(AppDbContext db, ILogger<PlayersController> log)
    {
        _db = db;
        _log = log;
    }

    private static PlayerDto ToDto(Player p) =>
        new(p.Id, p.TeamId, p.DisplayName, p.JerseyNumber, p.BirthYear, p.Position, p.IsActive, p.CreatedAt);

    private async Task<(Team? team, IActionResult? error)> LoadOwnedTeam(Guid teamId, CancellationToken ct)
        => await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);

    [HttpGet]
    public async Task<IActionResult> List(Guid teamId, CancellationToken ct)
    {
        var (team, err) = await LoadOwnedTeam(teamId, ct);
        if (err is not null) return err;

        var players = await _db.Players
            .Where(p => p.TeamId == teamId)
            .OrderBy(p => p.DisplayName)
            .ToListAsync(ct);

        return Ok(players.Select(ToDto));
    }

    [HttpGet("{playerId:guid}")]
    public async Task<IActionResult> Get(Guid teamId, Guid playerId, CancellationToken ct)
    {
        var (team, err) = await LoadOwnedTeam(teamId, ct);
        if (err is not null) return err;

        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId && p.TeamId == teamId, ct);
        if (player is null) return NotFound();
        return Ok(ToDto(player));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid teamId, [FromBody] CreatePlayerRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (team, err) = await LoadOwnedTeam(teamId, ct);
        if (err is not null) return err;

        var player = new Player
        {
            TeamId = teamId,
            DisplayName = request.DisplayName.Trim(),
            JerseyNumber = request.JerseyNumber,
            BirthYear = request.BirthYear,
            Position = string.IsNullOrWhiteSpace(request.Position) ? null : request.Position.Trim(),
        };
        _db.Players.Add(player);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("players.created {PlayerId} {TeamId}", player.Id, teamId);
        return CreatedAtAction(nameof(Get), new { teamId, playerId = player.Id }, ToDto(player));
    }

    [HttpPut("{playerId:guid}")]
    public async Task<IActionResult> Update(Guid teamId, Guid playerId, [FromBody] UpdatePlayerRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (team, err) = await LoadOwnedTeam(teamId, ct);
        if (err is not null) return err;

        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId && p.TeamId == teamId, ct);
        if (player is null) return NotFound();

        player.DisplayName = request.DisplayName.Trim();
        player.JerseyNumber = request.JerseyNumber;
        player.BirthYear = request.BirthYear;
        player.Position = string.IsNullOrWhiteSpace(request.Position) ? null : request.Position.Trim();
        player.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(player));
    }

    [HttpDelete("{playerId:guid}")]
    public async Task<IActionResult> Delete(Guid teamId, Guid playerId, CancellationToken ct)
    {
        var (team, err) = await LoadOwnedTeam(teamId, ct);
        if (err is not null) return err;

        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId && p.TeamId == teamId, ct);
        if (player is null) return NotFound();

        player.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("players.deleted {PlayerId} {TeamId}", playerId, teamId);
        return NoContent();
    }

    /// <summary>
    /// Per-player attendance history. Returns one row per team session
    /// (most-recent first) with the player's status; sessions with no
    /// attendance record fall through as Absent so the timeline has no
    /// gaps. Used by the player profile page.
    /// </summary>
    [HttpGet("{playerId:guid}/attendance")]
    public async Task<IActionResult> Attendance(Guid teamId, Guid playerId, CancellationToken ct)
    {
        var (_, _, err) = await TeamScope.RequireOwnedPlayer(this, _db, teamId, playerId, ct);
        if (err is not null) return err;

        var rows = await _db.Sessions
            .Where(s => s.TeamId == teamId)
            .OrderByDescending(s => s.ScheduledAt)
            .Select(s => new
            {
                s.Id,
                s.ScheduledAt,
                s.Type,
                s.Location,
                s.Focus,
                Record = _db.AttendanceRecords
                    .Where(a => a.SessionId == s.Id && a.PlayerId == playerId)
                    .Select(a => new { a.Status, a.Note })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var dto = rows.Select(r => new PlayerAttendanceRowDto(
            r.Id, r.ScheduledAt, r.Type, r.Location, r.Focus,
            r.Record is null ? AttendanceStatus.Absent : r.Record.Status,
            r.Record?.Note));
        return Ok(dto);
    }
}
