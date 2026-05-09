using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Tests.TestInfra;
using ForgeRise.Api.Teams.Contracts;
using ForgeRise.Api.WelfareModule.Contracts;
using Xunit;

namespace ForgeRise.Api.Tests.Welfare;

/// <summary>
/// Team activity feed: aggregates self-submitted check-ins, self-reported
/// incidents, and invite redemptions into one read-only stream coaches can
/// poll. Coach-recorded events are intentionally excluded — coaches don't
/// need to be notified of their own actions.
/// </summary>
public class TeamActivityTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public TeamActivityTests(ForgeRiseFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthenticatedClient(string emailPrefix)
    {
        var client = _factory.CreateDefaultClient(new CookieJarHandler());
        var resp = await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"{emailPrefix}-{Guid.NewGuid():n}@example.com",
            password = "Correct horse battery staple",
            displayName = emailPrefix,
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return client;
    }

    private static async Task<TeamDto> CreateTeam(HttpClient client, string name, string code)
    {
        var resp = await client.PostAsJsonAsync("/teams", new { name, code });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<TeamDto>())!;
    }

    private static async Task<PlayerDto> AddPlayer(HttpClient client, Guid teamId, string name)
    {
        var resp = await client.PostAsJsonAsync($"/teams/{teamId}/players",
            new { displayName = name, jerseyNumber = 9, position = "SH" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<PlayerDto>())!;
    }

    private static async Task<PlayerInviteDto> CreateInvite(HttpClient client, Guid teamId, Guid playerId)
    {
        var resp = await client.PostAsync($"/teams/{teamId}/players/{playerId}/invites", null);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<PlayerInviteDto>())!;
    }

    [Fact]
    public async Task Activity_includes_self_checkin_self_incident_and_redemption_excludes_coach_events()
    {
        var coach = await AuthenticatedClient("coach-act");
        var player = await AuthenticatedClient("player-act");
        var team = await CreateTeam(coach, "Owls", $"owls-{Guid.NewGuid():n}".Substring(0, 12));
        var roster = await AddPlayer(coach, team.Id, "Self Filer");

        // Coach-recorded incident — should NOT appear in the feed.
        var coachIncident = await coach.PostAsJsonAsync(
            $"/teams/{team.Id}/players/{roster.Id}/incidents",
            new { severity = (int)IncidentSeverity.Medium, summary = "Coach noted bruise" });
        Assert.Equal(HttpStatusCode.Created, coachIncident.StatusCode);

        // Player redeems invite — should appear.
        var invite = await CreateInvite(coach, team.Id, roster.Id);
        var redeem = await player.PostAsJsonAsync("/player-invites/redeem", new { code = invite.Code });
        redeem.EnsureSuccessStatusCode();

        // Player self-checks-in — should appear.
        var checkin = await player.PostAsJsonAsync(
            $"/me/players/{roster.Id}/checkins",
            new { sleepHours = 7.5, sorenessScore = 2, moodScore = 4, stressScore = 2, fatigueScore = 2 });
        Assert.Equal(HttpStatusCode.Created, checkin.StatusCode);

        // Player self-reports incident — should appear, unacknowledged.
        var selfIncident = await player.PostAsJsonAsync(
            $"/me/players/{roster.Id}/incidents",
            new { severity = (int)IncidentSeverity.Low, summary = "Tight hamstring" });
        Assert.Equal(HttpStatusCode.Created, selfIncident.StatusCode);

        var feed = await coach.GetFromJsonAsync<List<TeamActivityEventDto>>(
            $"/teams/{team.Id}/activity");
        Assert.NotNull(feed);

        var kinds = feed!.Select(e => e.Kind).ToList();
        Assert.Contains(TeamActivityKinds.CheckInSelfSubmitted, kinds);
        Assert.Contains(TeamActivityKinds.IncidentSelfReported, kinds);
        Assert.Contains(TeamActivityKinds.InviteRedeemed, kinds);

        // Coach-recorded incident summary must not surface as a self-event.
        Assert.DoesNotContain(feed, e =>
            e.Kind == TeamActivityKinds.IncidentSelfReported && e.Summary == "Coach noted bruise");

        var selfIncidentEvent = Assert.Single(feed,
            e => e.Kind == TeamActivityKinds.IncidentSelfReported);
        Assert.Equal("Tight hamstring", selfIncidentEvent.Summary);
        Assert.False(selfIncidentEvent.Acknowledged);

        // Sorted by At descending.
        for (var i = 1; i < feed.Count; i++)
            Assert.True(feed[i - 1].At >= feed[i].At, "feed must be sorted newest-first");
    }

    [Fact]
    public async Task Activity_respects_since_filter()
    {
        var coach = await AuthenticatedClient("coach-since");
        var player = await AuthenticatedClient("player-since");
        var team = await CreateTeam(coach, "Hawks", $"hawks-{Guid.NewGuid():n}".Substring(0, 12));
        var roster = await AddPlayer(coach, team.Id, "Self Filer");
        var invite = await CreateInvite(coach, team.Id, roster.Id);
        await player.PostAsJsonAsync("/player-invites/redeem", new { code = invite.Code });
        await player.PostAsJsonAsync(
            $"/me/players/{roster.Id}/checkins",
            new { sleepHours = 7.5, sorenessScore = 2, moodScore = 4, stressScore = 2, fatigueScore = 2 });

        // Future cutoff — empty.
        var future = DateTimeOffset.UtcNow.AddYears(1).ToString("o");
        var empty = await coach.GetFromJsonAsync<List<TeamActivityEventDto>>(
            $"/teams/{team.Id}/activity?since={Uri.EscapeDataString(future)}");
        Assert.Empty(empty!);
    }

    [Fact]
    public async Task Activity_is_team_scoped_to_owners_only()
    {
        var coach = await AuthenticatedClient("coach-scope");
        var stranger = await AuthenticatedClient("stranger-scope");
        var team = await CreateTeam(coach, "Otters", $"otters-{Guid.NewGuid():n}".Substring(0, 12));

        var resp = await stranger.GetAsync($"/teams/{team.Id}/activity");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
