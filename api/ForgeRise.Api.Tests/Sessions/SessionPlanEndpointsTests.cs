using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Sessions.Contracts;
using ForgeRise.Api.Tests.TestInfra;
using ForgeRise.Api.Welfare;
using Xunit;

namespace ForgeRise.Api.Tests.Sessions;

public class SessionPlanEndpointsTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public SessionPlanEndpointsTests(ForgeRiseFactory factory) => _factory = factory;

    private async Task<(HttpClient client, Guid teamId, Guid playerId)> Seed(string prefix)
    {
        var client = _factory.CreateDefaultClient(new CookieJarHandler());
        await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"{prefix}-{Guid.NewGuid():n}@example.com",
            password = "Correct horse battery staple",
            displayName = prefix,
        });
        var t = await client.PostAsJsonAsync("/teams", new { name = "Squad", code = $"{prefix}-sq" });
        var team = await t.Content.ReadFromJsonAsync<ForgeRise.Api.Teams.Contracts.TeamDto>();
        var p = await client.PostAsJsonAsync($"/teams/{team!.Id}/players", new { displayName = "Pat" });
        var player = await p.Content.ReadFromJsonAsync<ForgeRise.Api.Teams.Contracts.PlayerDto>();
        return (client, team.Id, player!.Id);
    }

    [Fact]
    public async Task Generate_with_no_data_returns_default_plan()
    {
        var (client, teamId, _) = await Seed("nia");

        var resp = await client.PostAsJsonAsync($"/teams/{teamId}/session-plans/generate", new { });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<SessionPlanDto>();
        Assert.Equal(5, dto!.Blocks.Count);
        Assert.Equal("General conditioning + skills", dto.Focus);
        Assert.Empty(dto.ReadinessSnapshot);
    }

    [Fact]
    public async Task Generate_after_checkin_includes_readiness_snapshot_without_raw()
    {
        var (client, teamId, playerId) = await Seed("opal");

        // Record a high-risk check-in. Raw fields must not appear in the plan response.
        await client.PostAsJsonAsync($"/teams/{teamId}/players/{playerId}/checkins", new
        {
            sleepHours = 3.0,
            sorenessScore = 5,
            moodScore = 1,
            stressScore = 5,
            fatigueScore = 5,
            injuryNotes = "REDACTABLE_RAW_FIELD",
        });

        var resp = await client.PostAsJsonAsync($"/teams/{teamId}/session-plans/generate", new { focus = "Tackle technique" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var raw = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("REDACTABLE_RAW_FIELD", raw);
        Assert.DoesNotContain("sleepHours", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("injuryNotes", raw, StringComparison.OrdinalIgnoreCase);

        var dto = await resp.Content.ReadFromJsonAsync<SessionPlanDto>();
        Assert.Equal("Tackle technique", dto!.Focus);
        Assert.Single(dto.ReadinessSnapshot);
        Assert.Equal(SafeCategory.RecoveryFocus, dto.ReadinessSnapshot[0].Category);
        Assert.All(dto.Blocks, b => Assert.Equal("Recovery emphasis", b.Intensity));
    }

    [Fact]
    public async Task Generate_uses_most_recent_reviewed_session_focus_when_no_override()
    {
        var (client, teamId, _) = await Seed("paz");

        var s = await client.PostAsJsonAsync($"/teams/{teamId}/sessions", new
        {
            scheduledAt = DateTimeOffset.UtcNow.AddDays(-1),
            durationMinutes = 60,
            type = SessionType.Training,
            focus = "Maul defence",
        });
        var session = await s.Content.ReadFromJsonAsync<SessionDto>();
        await client.PostAsJsonAsync($"/teams/{teamId}/sessions/{session!.Id}/review",
            new { reviewNotes = "Maul defence improved." });

        var resp = await client.PostAsJsonAsync($"/teams/{teamId}/session-plans/generate", new { });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<SessionPlanDto>();
        Assert.Equal("Maul defence", dto!.Focus);
        Assert.Equal(session.Id, dto.BasedOnSessionId);
    }

    [Fact]
    public async Task Non_owner_cannot_generate_or_list()
    {
        var (_, teamId, _) = await Seed("quin");
        var (stranger, _, _) = await Seed("ruth");

        var gen = await stranger.PostAsJsonAsync($"/teams/{teamId}/session-plans/generate", new { });
        Assert.Equal(HttpStatusCode.Forbidden, gen.StatusCode);

        var list = await stranger.GetAsync($"/teams/{teamId}/session-plans");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Generated_plan_includes_drill_recommendations()
    {
        var (client, teamId, _) = await Seed("sage");

        var resp = await client.PostAsJsonAsync(
            $"/teams/{teamId}/session-plans/generate",
            new { focus = "scrum sequencing" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var dto = await resp.Content.ReadFromJsonAsync<SessionPlanDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.Recommendations.Count >= 2);
        // Focus keyword steers at least one recommendation toward the scrum drill.
        Assert.Contains(dto.Recommendations, r => r.DrillId == "scrum-engage");
        Assert.All(dto.Recommendations, r => Assert.False(string.IsNullOrWhiteSpace(r.Rationale)));
    }

    [Fact]
    public async Task Generated_plan_surfaces_recent_self_incident_count()
    {
        var coach = _factory.CreateDefaultClient(new CookieJarHandler());
        await coach.PostAsJsonAsync("/auth/register", new
        {
            email = $"coach-rsi-{Guid.NewGuid():n}@example.com",
            password = "Correct horse battery staple",
            displayName = "coach-rsi",
        });
        var t = await coach.PostAsJsonAsync("/teams", new { name = "Squad", code = $"rsi-{Guid.NewGuid():n}".Substring(0, 12) });
        var team = (await t.Content.ReadFromJsonAsync<ForgeRise.Api.Teams.Contracts.TeamDto>())!;

        var p = await coach.PostAsJsonAsync($"/teams/{team.Id}/players", new { displayName = "Sam Self" });
        var player = (await p.Content.ReadFromJsonAsync<ForgeRise.Api.Teams.Contracts.PlayerDto>())!;

        // Player redeems their invite then self-reports two incidents.
        var inv = await coach.PostAsync($"/teams/{team.Id}/players/{player.Id}/invites", null);
        var invite = (await inv.Content.ReadFromJsonAsync<ForgeRise.Api.WelfareModule.Contracts.PlayerInviteDto>())!;
        var playerClient = _factory.CreateDefaultClient(new CookieJarHandler());
        await playerClient.PostAsJsonAsync("/auth/register", new
        {
            email = $"player-rsi-{Guid.NewGuid():n}@example.com",
            password = "Correct horse battery staple",
            displayName = "player-rsi",
        });
        (await playerClient.PostAsJsonAsync("/player-invites/redeem", new { code = invite.Code })).EnsureSuccessStatusCode();
        (await playerClient.PostAsJsonAsync($"/me/players/{player.Id}/incidents",
            new { severity = (int)IncidentSeverity.Low, summary = "Stiff back" })).EnsureSuccessStatusCode();
        (await playerClient.PostAsJsonAsync($"/me/players/{player.Id}/incidents",
            new { severity = (int)IncidentSeverity.Medium, summary = "Sore shoulder" })).EnsureSuccessStatusCode();

        var resp = await coach.PostAsJsonAsync($"/teams/{team.Id}/session-plans/generate", new { });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = (await resp.Content.ReadFromJsonAsync<SessionPlanDto>())!;
        Assert.Equal(2, dto.RecentSelfIncidentCount);

        // Roundtrip via Get.
        var fetched = await coach.GetFromJsonAsync<SessionPlanDto>(
            $"/teams/{team.Id}/session-plans/{dto.Id}");
        Assert.Equal(2, fetched!.RecentSelfIncidentCount);
    }

    [Fact]
    public async Task Adopt_creates_a_session_and_stamps_the_plan()
    {
        var (client, teamId, _) = await Seed("ad1");

        var gen = await client.PostAsJsonAsync($"/teams/{teamId}/session-plans/generate",
            new { focus = "Lineout" });
        gen.EnsureSuccessStatusCode();
        var plan = (await gen.Content.ReadFromJsonAsync<SessionPlanDto>())!;
        Assert.Null(plan.AdoptedAt);

        var scheduled = DateTimeOffset.UtcNow.AddDays(2);
        var adopt = await client.PostAsJsonAsync(
            $"/teams/{teamId}/session-plans/{plan.Id}/adopt",
            new
            {
                scheduledAt = scheduled,
                durationMinutes = 90,
                type = (int)Data.Entities.SessionType.Training,
                location = "Main pitch",
            });
        Assert.Equal(HttpStatusCode.OK, adopt.StatusCode);
        var adopted = (await adopt.Content.ReadFromJsonAsync<SessionPlanDto>())!;
        Assert.NotNull(adopted.AdoptedAt);
        Assert.NotNull(adopted.AdoptedSessionId);

        // The session that was created carries the plan's focus and a notes
        // digest of the recommended drills.
        var sessions = await client.GetFromJsonAsync<List<SessionDto>>(
            $"/teams/{teamId}/sessions");
        var session = Assert.Single(sessions!, s => s.Id == adopted.AdoptedSessionId);
        Assert.Equal("Lineout", session.Focus);
        Assert.NotNull(session.ReviewNotes);
        Assert.Contains("Adopted from session plan", session.ReviewNotes!);

        // Adopting twice is rejected.
        var again = await client.PostAsJsonAsync(
            $"/teams/{teamId}/session-plans/{plan.Id}/adopt",
            new
            {
                scheduledAt = scheduled,
                durationMinutes = 90,
                type = (int)Data.Entities.SessionType.Training,
            });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }
}
