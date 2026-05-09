using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Sessions;
using ForgeRise.Api.WelfareModule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Controllers;

/// <summary>
/// Per-team overrides on the static <see cref="DrillCatalogue"/>. A coach can
/// mark a drill as a favourite (recommender prioritises it) or exclude it
/// (recommender filters it out). Pure planning preference — never feeds the
/// welfare audit log.
/// </summary>
public sealed record DrillCataloguePrefDto(
    string DrillId,
    string Title,
    string Description,
    int DurationMinutes,
    IReadOnlyList<string> Tags,
    string? Status); // "favourite" | "exclude" | null

public sealed record SetDrillPreferenceRequest(string Status);

[ApiController]
[Authorize]
[Route("teams/{teamId:guid}/drill-preferences")]
public sealed class DrillPreferencesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public DrillPreferencesController(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid teamId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var existing = await _db.TeamDrillPreferences
            .Where(p => p.TeamId == teamId)
            .ToDictionaryAsync(p => p.DrillId, p => p.Status, ct);

        var rows = DrillCatalogue.All.Select(d =>
        {
            string? status = existing.TryGetValue(d.Id, out var s)
                ? (s == DrillPreferenceStatus.Favourite ? "favourite" : "exclude")
                : null;
            return new DrillCataloguePrefDto(d.Id, d.Title, d.Description, d.DurationMinutes, d.Tags, status);
        }).ToList();

        return Ok(rows);
    }

    [HttpPut("{drillId}")]
    public async Task<IActionResult> Set(Guid teamId, string drillId, [FromBody] SetDrillPreferenceRequest request, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        if (DrillCatalogue.All.All(d => d.Id != drillId)) return NotFound();

        DrillPreferenceStatus parsed = (request?.Status ?? string.Empty).ToLowerInvariant() switch
        {
            "favourite" => DrillPreferenceStatus.Favourite,
            "exclude" => DrillPreferenceStatus.Exclude,
            _ => (DrillPreferenceStatus)(-1),
        };
        if ((int)parsed < 0)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["status"] = new[] { "status must be 'favourite' or 'exclude'" },
            }));
        }

        var now = _time.GetUtcNow();
        var row = await _db.TeamDrillPreferences
            .FirstOrDefaultAsync(p => p.TeamId == teamId && p.DrillId == drillId, ct);
        if (row is null)
        {
            _db.TeamDrillPreferences.Add(new TeamDrillPreference
            {
                TeamId = teamId,
                DrillId = drillId,
                Status = parsed,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            row.Status = parsed;
            row.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{drillId}")]
    public async Task<IActionResult> Clear(Guid teamId, string drillId, CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        var row = await _db.TeamDrillPreferences
            .FirstOrDefaultAsync(p => p.TeamId == teamId && p.DrillId == drillId, ct);
        if (row is null) return NoContent();

        _db.TeamDrillPreferences.Remove(row);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
