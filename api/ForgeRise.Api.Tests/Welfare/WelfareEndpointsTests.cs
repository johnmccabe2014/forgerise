using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Tests.TestInfra;
using ForgeRise.Api.Welfare;
using ForgeRise.Api.WelfareModule.Contracts;
using Xunit;

namespace ForgeRise.Api.Tests.Welfare;

public class WelfareEndpointsTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public WelfareEndpointsTests(ForgeRiseFactory factory) => _factory = factory;

    private async Task<(HttpClient client, Guid teamId, Guid playerId)> SeedTeamAndPlayer(string prefix)
    {
        var client = _factory.CreateDefaultClient(new CookieJarHandler());
        var register = await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"{prefix}-{Guid.NewGuid():n}@example.com",
            password = "Correct horse battery staple",
            displayName = prefix,
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var t = await client.PostAsJsonAsync("/teams", new { name = "Squad", code = $"{prefix}-sq" });
        var team = await t.Content.ReadFromJsonAsync<ForgeRise.Api.Teams.Contracts.TeamDto>();

        var p = await client.PostAsJsonAsync($"/teams/{team!.Id}/players", new { displayName = "Pat" });
        var player = await p.Content.ReadFromJsonAsync<ForgeRise.Api.Teams.Contracts.PlayerDto>();

        return (client, team.Id, player!.Id);
    }

    [Fact]
    public async Task Create_checkin_returns_safe_summary_and_omits_raw()
    {
        var (client, teamId, playerId) = await SeedTeamAndPlayer("alex");

        var resp = await client.PostAsJsonAsync($"/teams/{teamId}/players/{playerId}/checkins", new
        {
            sleepHours = 4.0,
            sorenessScore = 4,
            moodScore = 2,
            stressScore = 4,
            fatigueScore = 4,
            injuryNotes = "tight hamstring",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var raw = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("tight hamstring", raw);
        Assert.DoesNotContain("sleepHours", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("injuryNotes", raw, StringComparison.OrdinalIgnoreCase);

        var dto = await resp.Content.ReadFromJsonAsync<CheckInSummaryDto>();
        Assert.NotNull(dto);
        // 4h sleep (3) + soreness 4 (2) + mood 2 (2) + stress 4 (2) + fatigue 4 (2) = 11 -> RecoveryFocus.
        Assert.Equal(SafeCategory.RecoveryFocus, dto!.Category);
    }

    [Fact]
    public async Task Team_readiness_returns_safe_categories_only()
    {
        var (client, teamId, playerId) = await SeedTeamAndPlayer("bree");

        await client.PostAsJsonAsync($"/teams/{teamId}/players/{playerId}/checkins", new
        {
            sleepHours = 8.0, sorenessScore = 1, moodScore = 5, stressScore = 1, fatigueScore = 1,
            injuryNotes = "secret note",
        });

        var resp = await client.GetAsync($"/teams/{teamId}/readiness");
        resp.EnsureSuccessStatusCode();
        var raw = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret note", raw);
        Assert.DoesNotContain("sleepHours", raw, StringComparison.OrdinalIgnoreCase);

        var rows = await resp.Content.ReadFromJsonAsync<List<TeamReadinessDto>>();
        Assert.Single(rows!);
        Assert.Equal(SafeCategory.Ready, rows![0].Category);
        Assert.Equal("Ready", rows[0].CategoryLabel);
        Assert.False(rows[0].SubmittedBySelf);
    }

    [Fact]
    public async Task Raw_endpoint_returns_raw_and_writes_audit_row()
    {
        var (client, teamId, playerId) = await SeedTeamAndPlayer("cal");

        var create = await client.PostAsJsonAsync($"/teams/{teamId}/players/{playerId}/checkins", new
        {
            sleepHours = 7.0, sorenessScore = 2, moodScore = 4, stressScore = 2, fatigueScore = 2,
            injuryNotes = "minor knock",
        });
        var summary = await create.Content.ReadFromJsonAsync<CheckInSummaryDto>();

        var rawResp = await client.GetAsync($"/teams/{teamId}/players/{playerId}/checkins/{summary!.Id}/raw");
        rawResp.EnsureSuccessStatusCode();
        var rawDto = await rawResp.Content.ReadFromJsonAsync<CheckInRawDto>();
        Assert.Equal("minor knock", rawDto!.InjuryNotes);
        Assert.Equal(7.0, rawDto.SleepHours);

        var auditResp = await client.GetAsync($"/teams/{teamId}/welfare-audit");
        auditResp.EnsureSuccessStatusCode();
        var audit = await auditResp.Content.ReadFromJsonAsync<List<AuditEntryDto>>();
        Assert.Contains(audit!, a => a.Action == WelfareAuditAction.ReadRawCheckIn && a.SubjectId == summary.Id);
        // Display names are joined in for the coach-readable audit page.
        var raw = Assert.Single(audit!, a => a.Action == WelfareAuditAction.ReadRawCheckIn);
        Assert.False(string.IsNullOrEmpty(raw.ActorDisplayName));
        Assert.False(string.IsNullOrEmpty(raw.PlayerDisplayName));
    }

    [Fact]
    public async Task Audit_log_filters_by_action_and_player()
    {
        var (client, teamId, playerId) = await SeedTeamAndPlayer("filt");
        var create = await client.PostAsJsonAsync($"/teams/{teamId}/players/{playerId}/checkins", new
        {
            sleepHours = 7.0, sorenessScore = 2, moodScore = 4, stressScore = 2, fatigueScore = 2,
        });
        var summary = await create.Content.ReadFromJsonAsync<CheckInSummaryDto>();
        (await client.GetAsync($"/teams/{teamId}/players/{playerId}/checkins/{summary!.Id}/raw"))
            .EnsureSuccessStatusCode();

        var byAction = await client.GetFromJsonAsync<List<AuditEntryDto>>(
            $"/teams/{teamId}/welfare-audit?action=ReadRawCheckIn");
        Assert.NotEmpty(byAction!);
        Assert.All(byAction!, a => Assert.Equal(WelfareAuditAction.ReadRawCheckIn, a.Action));

        var byPlayer = await client.GetFromJsonAsync<List<AuditEntryDto>>(
            $"/teams/{teamId}/welfare-audit?playerId={playerId}");
        Assert.All(byPlayer!, a => Assert.Equal(playerId, a.PlayerId));

        var bogus = await client.GetAsync($"/teams/{teamId}/welfare-audit?action=NotARealAction");
        Assert.Equal(HttpStatusCode.BadRequest, bogus.StatusCode);
    }

    [Fact]
    public async Task Audit_log_paginates_with_skip_and_take()
    {
        var (client, teamId, playerId) = await SeedTeamAndPlayer("page");
        // Generate 5 raw-read audit events (one create + repeated raw reads).
        var create = await client.PostAsJsonAsync($"/teams/{teamId}/players/{playerId}/checkins", new
        {
            sleepHours = 7.0, sorenessScore = 2, moodScore = 4, stressScore = 2, fatigueScore = 2,
        });
        var summary = await create.Content.ReadFromJsonAsync<CheckInSummaryDto>();
        for (var i = 0; i < 5; i++)
        {
            (await client.GetAsync($"/teams/{teamId}/players/{playerId}/checkins/{summary!.Id}/raw"))
                .EnsureSuccessStatusCode();
        }

        var first = await client.GetFromJsonAsync<List<AuditEntryDto>>(
            $"/teams/{teamId}/welfare-audit?take=2");
        Assert.Equal(2, first!.Count);

        var second = await client.GetFromJsonAsync<List<AuditEntryDto>>(
            $"/teams/{teamId}/welfare-audit?skip=2&take=2");
        Assert.Equal(2, second!.Count);
        Assert.DoesNotContain(second, s => first.Any(f => f.Id == s.Id));

        var bad = await client.GetAsync($"/teams/{teamId}/welfare-audit?take=500");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Audit_log_exports_filtered_csv()
    {
        var (client, teamId, playerId) = await SeedTeamAndPlayer("csv");
        var create = await client.PostAsJsonAsync($"/teams/{teamId}/players/{playerId}/checkins", new
        {
            sleepHours = 7.0, sorenessScore = 2, moodScore = 4, stressScore = 2, fatigueScore = 2,
        });
        var summary = await create.Content.ReadFromJsonAsync<CheckInSummaryDto>();
        (await client.GetAsync($"/teams/{teamId}/players/{playerId}/checkins/{summary!.Id}/raw"))
            .EnsureSuccessStatusCode();

        var resp = await client.GetAsync(
            $"/teams/{teamId}/welfare-audit/export.csv?action=ReadRawCheckIn");
        resp.EnsureSuccessStatusCode();
        Assert.StartsWith("text/csv", resp.Content.Headers.ContentType?.MediaType is null
            ? resp.Content.Headers.ContentType?.ToString() ?? ""
            : resp.Content.Headers.ContentType.ToString(), StringComparison.OrdinalIgnoreCase);
        var disposition = resp.Content.Headers.ContentDisposition?.FileNameStar
            ?? resp.Content.Headers.ContentDisposition?.FileName;
        Assert.NotNull(disposition);
        Assert.Contains("welfare-audit-", disposition!);

        var body = await resp.Content.ReadAsStringAsync();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("at,action,actor,player,actorUserId,playerId", lines[0]);
        Assert.True(lines.Length >= 2, "expected at least one data row");
        Assert.Contains("ReadRawCheckIn", lines[1]);

        var bogus = await client.GetAsync(
            $"/teams/{teamId}/welfare-audit/export.csv?action=NotARealAction");
        Assert.Equal(HttpStatusCode.BadRequest, bogus.StatusCode);
    }

    [Fact]
    public async Task Purge_raw_clears_fields_keeps_category()
    {
        var (client, teamId, playerId) = await SeedTeamAndPlayer("dan");
        var create = await client.PostAsJsonAsync($"/teams/{teamId}/players/{playerId}/checkins", new
        {
            sleepHours = 6.5, sorenessScore = 3, moodScore = 4, stressScore = 3, fatigueScore = 3,
            injuryNotes = "delete me",
        });
        var summary = await create.Content.ReadFromJsonAsync<CheckInSummaryDto>();

        var purge = await client.PostAsync(
            $"/teams/{teamId}/players/{playerId}/checkins/{summary!.Id}/purge-raw", content: null);
        Assert.Equal(HttpStatusCode.NoContent, purge.StatusCode);

        var rawResp = await client.GetAsync($"/teams/{teamId}/players/{playerId}/checkins/{summary.Id}/raw");
        var rawDto = await rawResp.Content.ReadFromJsonAsync<CheckInRawDto>();
        Assert.Null(rawDto!.InjuryNotes);
        Assert.Null(rawDto.SleepHours);
        Assert.NotNull(rawDto.RawPurgedAt);

        // Category snapshot survives purge.
        var summaryResp = await client.GetAsync($"/teams/{teamId}/players/{playerId}/checkins/{summary.Id}");
        var after = await summaryResp.Content.ReadFromJsonAsync<CheckInSummaryDto>();
        Assert.Equal(summary.Category, after!.Category);
    }

    [Fact]
    public async Task Non_owner_cannot_read_or_write_welfare()
    {
        var (owner, teamId, playerId) = await SeedTeamAndPlayer("erin");
        var (stranger, _, _) = await SeedTeamAndPlayer("frank");

        var post = await stranger.PostAsJsonAsync($"/teams/{teamId}/players/{playerId}/checkins",
            new { sleepHours = 8.0, sorenessScore = 1, moodScore = 5, stressScore = 1, fatigueScore = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);

        var readiness = await stranger.GetAsync($"/teams/{teamId}/readiness");
        Assert.Equal(HttpStatusCode.Forbidden, readiness.StatusCode);

        var audit = await stranger.GetAsync($"/teams/{teamId}/welfare-audit");
        Assert.Equal(HttpStatusCode.Forbidden, audit.StatusCode);
    }

    [Fact]
    public async Task Incident_list_omits_notes_and_raw_endpoint_audits()
    {
        var (client, teamId, playerId) = await SeedTeamAndPlayer("gail");

        var create = await client.PostAsJsonAsync(
            $"/teams/{teamId}/players/{playerId}/incidents",
            new { severity = 1, summary = "Knock to shin during contact drill", notes = "Confidential note" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var summary = await create.Content.ReadFromJsonAsync<IncidentSummaryDto>();

        var list = await client.GetAsync($"/teams/{teamId}/incidents");
        list.EnsureSuccessStatusCode();
        var listBody = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Confidential note", listBody);

        var raw = await client.GetAsync($"/teams/{teamId}/players/{playerId}/incidents/{summary!.Id}/raw");
        raw.EnsureSuccessStatusCode();
        var rawDto = await raw.Content.ReadFromJsonAsync<IncidentRawDto>();
        Assert.Equal("Confidential note", rawDto!.Notes);

        var audit = await client.GetAsync($"/teams/{teamId}/welfare-audit");
        var auditDto = await audit.Content.ReadFromJsonAsync<List<AuditEntryDto>>();
        Assert.Contains(auditDto!, a => a.Action == WelfareAuditAction.ReadRawIncident && a.SubjectId == summary.Id);
    }
}
