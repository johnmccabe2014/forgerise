using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Teams.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Controllers;

[ApiController]
[Route("teams")]
[Authorize]
public sealed class TeamsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<TeamsController> _log;

    public TeamsController(AppDbContext db, ILogger<TeamsController> log)
    {
        _db = db;
        _log = log;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        var teams = await _db.Teams
            .Where(t => t.OwnerUserId == userId)
            .OrderBy(t => t.Name)
            .Select(t => new TeamDto(t.Id, t.Name, t.Code, t.CreatedAt, t.Players.Count(p => p.DeletedAt == null)))
            .ToListAsync(ct);

        return Ok(teams);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        var team = await _db.Teams
            .Where(t => t.Id == id)
            .Select(t => new { Team = t, OwnerId = t.OwnerUserId, PlayerCount = t.Players.Count(p => p.DeletedAt == null) })
            .FirstOrDefaultAsync(ct);

        if (team is null) return NotFound();
        if (team.OwnerId != userId) return Forbid();

        return Ok(new TeamDto(team.Team.Id, team.Team.Name, team.Team.Code, team.Team.CreatedAt, team.PlayerCount));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        var code = request.Code.Trim();
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
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("teams.created {TeamId} {OwnerUserId}", team.Id, userId);
        return CreatedAtAction(nameof(Get), new { id = team.Id },
            new TeamDto(team.Id, team.Name, team.Code, team.CreatedAt, 0));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTeamRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (team is null) return NotFound();
        if (team.OwnerUserId != userId) return Forbid();

        team.Name = request.Name.Trim();
        await _db.SaveChangesAsync(ct);

        var count = await _db.Players.CountAsync(p => p.TeamId == id, ct);
        return Ok(new TeamDto(team.Id, team.Name, team.Code, team.CreatedAt, count));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        if (userId is null) return Unauthorized();

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (team is null) return NotFound();
        if (team.OwnerUserId != userId) return Forbid();

        team.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("teams.deleted {TeamId} {OwnerUserId}", id, userId);
        return NoContent();
    }
}
