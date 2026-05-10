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
    string? Status, // "favourite" | "exclude" | null
    DateTimeOffset? UpdatedAt = null,
    string? LastChangedByDisplayName = null);

public sealed record SetDrillPreferenceRequest(string Status);

public sealed record DrillPreferenceImportRequest(string Csv);

public sealed record DrillPreferenceImportError(int Line, string Reason);

public sealed record DrillPreferenceImportResult(
    int Applied,
    int Cleared,
    int Skipped,
    IReadOnlyList<DrillPreferenceImportError> Errors);

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
            .ToListAsync(ct);
        var byDrill = existing.ToDictionary(p => p.DrillId);
        var actorIds = existing
            .Where(p => p.LastChangedByUserId is not null)
            .Select(p => p.LastChangedByUserId!.Value)
            .Distinct()
            .ToArray();
        var nameById = actorIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var rows = DrillCatalogue.All.Select(d =>
        {
            if (!byDrill.TryGetValue(d.Id, out var pref))
            {
                return new DrillCataloguePrefDto(d.Id, d.Title, d.Description, d.DurationMinutes, d.Tags, null);
            }
            string status = pref.Status == DrillPreferenceStatus.Favourite ? "favourite" : "exclude";
            string? actor = pref.LastChangedByUserId is { } id && nameById.TryGetValue(id, out var n) ? n : null;
            return new DrillCataloguePrefDto(d.Id, d.Title, d.Description, d.DurationMinutes, d.Tags,
                status, pref.UpdatedAt, actor);
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
        var actor = User.TryGetUserId();
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
                LastChangedByUserId = actor,
            });
        }
        else
        {
            row.Status = parsed;
            row.UpdatedAt = now;
            row.LastChangedByUserId = actor;
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

    /// <summary>
    /// Bulk-set or clear preferences from a CSV body. Each non-empty line
    /// is "drillId,status" where status is favourite | exclude | clear. A
    /// header row ("drillId,status") is tolerated. Unknown drill ids and
    /// malformed rows are reported back rather than aborting the whole
    /// import — the goal is "apply what you can, tell me what failed".
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import(
        Guid teamId,
        [FromBody] DrillPreferenceImportRequest request,
        CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        if (request is null || string.IsNullOrWhiteSpace(request.Csv))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["csv"] = new[] { "csv body is required" },
            }));
        }

        var validIds = DrillCatalogue.All.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var existing = await _db.TeamDrillPreferences
            .Where(p => p.TeamId == teamId)
            .ToDictionaryAsync(p => p.DrillId, ct);
        var now = _time.GetUtcNow();
        var actor = User.TryGetUserId();
        var errors = new List<DrillPreferenceImportError>();
        var applied = 0;
        var cleared = 0;
        var skipped = 0;

        var lines = request.Csv.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i].Trim().TrimEnd('\r');
            if (raw.Length == 0) { skipped++; continue; }

            var parts = raw.Split(',');
            if (parts.Length < 2)
            {
                errors.Add(new DrillPreferenceImportError(i + 1, "expected drillId,status"));
                continue;
            }
            var drillId = parts[0].Trim();
            var statusText = parts[1].Trim().ToLowerInvariant();

            // Tolerate a header row.
            if (i == 0 && string.Equals(drillId, "drillId", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            if (!validIds.Contains(drillId))
            {
                errors.Add(new DrillPreferenceImportError(i + 1, $"unknown drill '{drillId}'"));
                continue;
            }

            if (statusText == "clear")
            {
                if (existing.TryGetValue(drillId, out var existingRow))
                {
                    _db.TeamDrillPreferences.Remove(existingRow);
                    existing.Remove(drillId);
                    cleared++;
                }
                else
                {
                    skipped++;
                }
                continue;
            }

            var parsed = statusText switch
            {
                "favourite" => DrillPreferenceStatus.Favourite,
                "exclude" => DrillPreferenceStatus.Exclude,
                _ => (DrillPreferenceStatus)(-1),
            };
            if ((int)parsed < 0)
            {
                errors.Add(new DrillPreferenceImportError(
                    i + 1, $"status must be favourite, exclude, or clear (got '{statusText}')"));
                continue;
            }

            if (existing.TryGetValue(drillId, out var row))
            {
                row.Status = parsed;
                row.UpdatedAt = now;
                row.LastChangedByUserId = actor;
            }
            else
            {
                var added = new TeamDrillPreference
                {
                    TeamId = teamId,
                    DrillId = drillId,
                    Status = parsed,
                    CreatedAt = now,
                    UpdatedAt = now,
                    LastChangedByUserId = actor,
                };
                _db.TeamDrillPreferences.Add(added);
                existing[drillId] = added;
            }
            applied++;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new DrillPreferenceImportResult(applied, cleared, skipped, errors));
    }
}
