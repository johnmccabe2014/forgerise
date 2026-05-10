using System.Globalization;
using System.Text;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.WelfareModule;
using ForgeRise.Api.WelfareModule.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Controllers;

[ApiController]
[Authorize]
[Route("teams/{teamId:guid}/welfare-audit")]
public sealed class WelfareAuditController : ControllerBase
{
    private readonly AppDbContext _db;
    public WelfareAuditController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(
        Guid teamId,
        [FromQuery] string? action,
        [FromQuery] Guid? playerId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        WelfareAuditAction? actionFilter = null;
        if (!string.IsNullOrWhiteSpace(action))
        {
            if (!Enum.TryParse<WelfareAuditAction>(action, ignoreCase: true, out var parsed))
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["action"] = new[] { "unknown action" },
                }));
            }
            actionFilter = parsed;
        }

        // Page-window defaults: 50 rows starting at offset 0, capped at 100 to
        // keep the audit page snappy. The page detects "has more" by checking
        // whether it received a full window.
        var skipValue = skip ?? 0;
        var takeValue = take ?? 50;
        if (skipValue < 0 || takeValue < 1 || takeValue > 100)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["paging"] = new[] { "skip must be >= 0 and take must be 1..100" },
            }));
        }

        var query = _db.WelfareAuditLogs
            .Where(a => _db.Players.Any(p => p.Id == a.PlayerId && p.TeamId == teamId));
        if (actionFilter is { } act) query = query.Where(a => a.Action == act);
        if (playerId is { } pid) query = query.Where(a => a.PlayerId == pid);
        if (from is { } f) query = query.Where(a => a.At >= f);
        if (to is { } t) query = query.Where(a => a.At <= t);

        var rows = await query
            .OrderByDescending(a => a.At)
            .Skip(skipValue)
            .Take(takeValue)
            .ToListAsync(ct);

        // Resolve actor + player display names in two single round-trips so
        // the page can render "Coach acknowledged Player" without N+1.
        var actorIds = rows.Select(r => r.ActorUserId).Distinct().ToArray();
        var playerIds = rows.Select(r => r.PlayerId).Distinct().ToArray();
        var actorById = actorIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var playerById = playerIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _db.Players
                .Where(p => playerIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);

        return Ok(rows.Select(a => new AuditEntryDto(
            a.Id, a.ActorUserId, a.PlayerId, a.SubjectId, a.Action, a.At,
            actorById.TryGetValue(a.ActorUserId, out var an) ? an : null,
            playerById.TryGetValue(a.PlayerId, out var pn) ? pn : null)));
    }

    // CSV export of the same filter set, capped at 5000 rows so an
    // accidental "export everything" can't OOM the API. Coaches can share
    // the file with medical staff without granting them app access. We
    // intentionally do NOT include subject ids — those reference raw
    // welfare records and are provenance-only per master.prompt §9.
    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        Guid teamId,
        [FromQuery] string? action,
        [FromQuery] Guid? playerId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var (_, err) = await TeamScope.RequireOwnedTeam(this, _db, teamId, ct);
        if (err is not null) return err;

        WelfareAuditAction? actionFilter = null;
        if (!string.IsNullOrWhiteSpace(action))
        {
            if (!Enum.TryParse<WelfareAuditAction>(action, ignoreCase: true, out var parsed))
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["action"] = new[] { "unknown action" },
                }));
            }
            actionFilter = parsed;
        }

        var query = _db.WelfareAuditLogs
            .Where(a => _db.Players.Any(p => p.Id == a.PlayerId && p.TeamId == teamId));
        if (actionFilter is { } act) query = query.Where(a => a.Action == act);
        if (playerId is { } pid) query = query.Where(a => a.PlayerId == pid);
        if (from is { } f) query = query.Where(a => a.At >= f);
        if (to is { } t) query = query.Where(a => a.At <= t);

        const int ExportCap = 5000;
        var rows = await query
            .OrderByDescending(a => a.At)
            .Take(ExportCap)
            .ToListAsync(ct);

        var actorIds = rows.Select(r => r.ActorUserId).Distinct().ToArray();
        var playerIds = rows.Select(r => r.PlayerId).Distinct().ToArray();
        var actorById = actorIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var playerById = playerIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _db.Players
                .Where(p => playerIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);

        var sb = new StringBuilder();
        sb.AppendLine("at,action,actor,player,actorUserId,playerId");
        foreach (var a in rows)
        {
            var actorName = actorById.TryGetValue(a.ActorUserId, out var an) ? an : a.ActorUserId.ToString();
            var playerName = playerById.TryGetValue(a.PlayerId, out var pn) ? pn : a.PlayerId.ToString();
            sb.Append(a.At.ToString("O", CultureInfo.InvariantCulture)).Append(',')
              .Append(CsvField(a.Action.ToString())).Append(',')
              .Append(CsvField(actorName)).Append(',')
              .Append(CsvField(playerName)).Append(',')
              .Append(a.ActorUserId).Append(',')
              .Append(a.PlayerId).Append('\n');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return File(bytes, "text/csv; charset=utf-8", $"welfare-audit-{stamp}.csv");
    }

    // RFC4180-ish field escaping: wrap in quotes if the value contains
    // a comma, quote, CR, or LF; double up any internal quotes.
    private static string CsvField(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
        if (!needsQuotes) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
