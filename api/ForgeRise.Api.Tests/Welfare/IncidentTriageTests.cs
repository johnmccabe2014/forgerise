using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Tests.TestInfra;
using ForgeRise.Api.Teams.Contracts;
using ForgeRise.Api.WelfareModule.Contracts;
using Xunit;

namespace ForgeRise.Api.Tests.Welfare;

/// <summary>
/// Coach triage of player-submitted incidents. Coach-recorded incidents
/// auto-acknowledge at creation; player-submitted reports require explicit ack.
/// </summary>
public class IncidentTriageTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public IncidentTriageTests(ForgeRiseFactory factory) => _factory = factory;

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
            new { displayName = name, jerseyNumber = 10, position = "FH" });
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
    public async Task Coach_recorded_incident_is_acknowledged_at_creation()
    {
        var coach = await AuthenticatedClient("coach-ack1");
        var team = await CreateTeam(coach, "Hawks", "hawks-ack1");
        var player = await AddPlayer(coach, team.Id, "Coach-Filed");

        var resp = await coach.PostAsJsonAsync(
            $"/teams/{team.Id}/players/{player.Id}/incidents",
            new { severity = (int)IncidentSeverity.Medium, summary = "Twisted ankle" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = (await resp.Content.ReadFromJsonAsync<IncidentSummaryDto>())!;
        Assert.False(created.SubmittedBySelf);
        Assert.NotNull(created.AcknowledgedAt);
    }

    [Fact]
    public async Task Self_reported_incident_is_unacknowledged_until_coach_acks()
    {
        var coach = await AuthenticatedClient("coach-ack2");
        var player = await AuthenticatedClient("player-ack2");
        var team = await CreateTeam(coach, "Falcons", "falcons-ack2");
        var roster = await AddPlayer(coach, team.Id, "Self-Filer");
        var invite = await CreateInvite(coach, team.Id, roster.Id);

        var redeem = await player.PostAsJsonAsync("/player-invites/redeem", new { code = invite.Code });
        redeem.EnsureSuccessStatusCode();

        var report = await player.PostAsJsonAsync(
            $"/me/players/{roster.Id}/incidents",
            new { severity = (int)IncidentSeverity.Low, summary = "Sore knee after sprints" });
        Assert.Equal(HttpStatusCode.Created, report.StatusCode);

        var list = await coach.GetFromJsonAsync<List<IncidentSummaryDto>>($"/teams/{team.Id}/incidents");
        var row = Assert.Single(list!);
        Assert.True(row.SubmittedBySelf);
        Assert.Null(row.AcknowledgedAt);

        var ack = await coach.PostAsync(
            $"/teams/{team.Id}/players/{roster.Id}/incidents/{row.Id}/acknowledge", null);
        Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
        var acked = (await ack.Content.ReadFromJsonAsync<IncidentSummaryDto>())!;
        Assert.NotNull(acked.AcknowledgedAt);

        // Idempotent: ack again returns the same timestamp.
        var ack2 = await coach.PostAsync(
            $"/teams/{team.Id}/players/{roster.Id}/incidents/{row.Id}/acknowledge", null);
        Assert.Equal(HttpStatusCode.OK, ack2.StatusCode);
        var acked2 = (await ack2.Content.ReadFromJsonAsync<IncidentSummaryDto>())!;
        Assert.Equal(acked.AcknowledgedAt, acked2.AcknowledgedAt);
    }

    [Fact]
    public async Task Stranger_cannot_acknowledge_incident()
    {
        var coach = await AuthenticatedClient("coach-ack3");
        var stranger = await AuthenticatedClient("stranger-ack3");
        var team = await CreateTeam(coach, "Otters", "otters-ack3");
        var roster = await AddPlayer(coach, team.Id, "Walled");

        var created = await coach.PostAsJsonAsync(
            $"/teams/{team.Id}/players/{roster.Id}/incidents",
            new { severity = (int)IncidentSeverity.Low, summary = "Bumped head" });
        var dto = (await created.Content.ReadFromJsonAsync<IncidentSummaryDto>())!;

        var resp = await stranger.PostAsync(
            $"/teams/{team.Id}/players/{roster.Id}/incidents/{dto.Id}/acknowledge", null);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task List_with_status_filter_separates_unread_and_acknowledged()
    {
        var coach = await AuthenticatedClient("coach-hist1");
        var player = await AuthenticatedClient("player-hist1");
        var team = await CreateTeam(coach, "Wrens", "wrens-hist1");

        // Self-reported incident A — leave unread.
        var rosterA = await AddPlayer(coach, team.Id, "Self A");
        var inviteA = await CreateInvite(coach, team.Id, rosterA.Id);
        (await player.PostAsJsonAsync("/player-invites/redeem", new { code = inviteA.Code }))
            .EnsureSuccessStatusCode();
        (await player.PostAsJsonAsync($"/me/players/{rosterA.Id}/incidents",
            new { severity = (int)IncidentSeverity.Low, summary = "Sore wrist" }))
            .EnsureSuccessStatusCode();

        // Coach-recorded incident B — auto-acknowledged at creation.
        var rosterB = await AddPlayer(coach, team.Id, "Coach B");
        (await coach.PostAsJsonAsync($"/teams/{team.Id}/players/{rosterB.Id}/incidents",
            new { severity = (int)IncidentSeverity.Medium, summary = "Bumped knee" }))
            .EnsureSuccessStatusCode();

        var unread = await coach.GetFromJsonAsync<List<IncidentSummaryDto>>(
            $"/teams/{team.Id}/incidents?status=unread");
        var unreadRow = Assert.Single(unread!);
        Assert.True(unreadRow.SubmittedBySelf);
        Assert.Null(unreadRow.AcknowledgedAt);

        var acked = await coach.GetFromJsonAsync<List<IncidentSummaryDto>>(
            $"/teams/{team.Id}/incidents?status=acknowledged");
        var ackedRow = Assert.Single(acked!);
        Assert.Equal("Bumped knee", ackedRow.Summary);
        Assert.NotNull(ackedRow.AcknowledgedAt);
        // Acknowledger name resolves on the history view.
        Assert.Equal("coach-hist1", ackedRow.AcknowledgedByDisplayName);

        var all = await coach.GetFromJsonAsync<List<IncidentSummaryDto>>(
            $"/teams/{team.Id}/incidents?status=all");
        Assert.Equal(2, all!.Count);
    }

    [Fact]
    public async Task Player_incident_timeline_includes_acknowledger_name()
    {
        var coach = await AuthenticatedClient("coach-tl1");
        var player = await AuthenticatedClient("player-tl1");
        var team = await CreateTeam(coach, "Eagles", "eagles-tl1");
        var roster = await AddPlayer(coach, team.Id, "Sam Self");

        // Player self-reports; coach acknowledges.
        var invite = await CreateInvite(coach, team.Id, roster.Id);
        (await player.PostAsJsonAsync("/player-invites/redeem", new { code = invite.Code }))
            .EnsureSuccessStatusCode();
        var post = await player.PostAsJsonAsync($"/me/players/{roster.Id}/incidents",
            new { severity = (int)IncidentSeverity.Low, summary = "Tight calf" });
        post.EnsureSuccessStatusCode();
        var created = (await post.Content.ReadFromJsonAsync<IncidentSummaryDto>())!;

        (await coach.PostAsync(
            $"/teams/{team.Id}/players/{roster.Id}/incidents/{created.Id}/acknowledge", null))
            .EnsureSuccessStatusCode();

        var timeline = await coach.GetFromJsonAsync<List<IncidentSummaryDto>>(
            $"/teams/{team.Id}/players/{roster.Id}/incidents");
        var row = Assert.Single(timeline!);
        Assert.Equal("Tight calf", row.Summary);
        Assert.NotNull(row.AcknowledgedAt);
        Assert.Equal("coach-tl1", row.AcknowledgedByDisplayName);
    }
}
