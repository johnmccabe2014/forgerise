using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Tests.TestInfra;
using ForgeRise.Api.Teams.Contracts;
using ForgeRise.Api.Welfare;
using ForgeRise.Api.WelfareModule.Contracts;
using Xunit;

namespace ForgeRise.Api.Tests.Teams;

/// <summary>
/// End-to-end self-service flow: coach issues a player invite → second user
/// redeems the code, becoming a linked player → linked user submits a self
/// check-in via /me and sees it in their own list.
/// </summary>
public class PlayerSelfServiceTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public PlayerSelfServiceTests(ForgeRiseFactory factory) => _factory = factory;

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

    private static async Task<PlayerInviteDto> CreatePlayerInvite(HttpClient client, Guid teamId, Guid playerId)
    {
        var resp = await client.PostAsync($"/teams/{teamId}/players/{playerId}/invites", null);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<PlayerInviteDto>())!;
    }

    [Fact]
    public async Task Player_can_redeem_invite_and_self_check_in()
    {
        var coach = await AuthenticatedClient("coach-ss");
        var player = await AuthenticatedClient("player-ss");

        var team = await CreateTeam(coach, "Lions SS", "lions-ss");
        var roster = await AddPlayer(coach, team.Id, "Sam Self");
        var invite = await CreatePlayerInvite(coach, team.Id, roster.Id);
        Assert.False(string.IsNullOrWhiteSpace(invite.Code));

        var redeem = await player.PostAsJsonAsync("/player-invites/redeem",
            new { code = invite.Code });
        redeem.EnsureSuccessStatusCode();
        var claim = (await redeem.Content.ReadFromJsonAsync<RedeemPlayerInviteResponse>())!;
        Assert.Equal(roster.Id, claim.PlayerId);
        Assert.Equal(team.Id, claim.TeamId);

        var mine = await player.GetFromJsonAsync<List<MyLinkedPlayerDto>>("/me/players");
        var only = Assert.Single(mine!);
        Assert.Equal(roster.Id, only.PlayerId);
        Assert.Equal("Sam Self", only.PlayerDisplayName);
        Assert.Equal("Lions SS", only.TeamName);

        var submit = await player.PostAsJsonAsync(
            $"/me/players/{roster.Id}/checkins",
            new { sleepHours = 7.5, sorenessScore = 2, moodScore = 4, stressScore = 2, fatigueScore = 2 });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var saved = (await submit.Content.ReadFromJsonAsync<MyCheckInDto>())!;
        Assert.True(saved.SubmittedBySelf);
        Assert.Equal(SafeCategory.Ready, saved.Category);

        var list = await player.GetFromJsonAsync<List<MyCheckInDto>>(
            $"/me/players/{roster.Id}/checkins");
        Assert.Single(list!);
        Assert.True(list![0].SubmittedBySelf);
    }

    [Fact]
    public async Task Redeem_rejects_unknown_revoked_consumed_codes()
    {
        var coach = await AuthenticatedClient("coach-rj");
        var player = await AuthenticatedClient("player-rj");
        var other = await AuthenticatedClient("other-rj");

        var team = await CreateTeam(coach, "Tigers SS", "tigers-ss");
        var roster = await AddPlayer(coach, team.Id, "Pat Player");

        var bad = await player.PostAsJsonAsync("/player-invites/redeem", new { code = "no-such-code" });
        Assert.Equal(HttpStatusCode.NotFound, bad.StatusCode);

        var invite = await CreatePlayerInvite(coach, team.Id, roster.Id);
        var revoke = await coach.DeleteAsync($"/teams/{team.Id}/players/{roster.Id}/invites/{invite.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        var revokedRedeem = await player.PostAsJsonAsync(
            "/player-invites/redeem", new { code = invite.Code });
        Assert.Equal(HttpStatusCode.Conflict, revokedRedeem.StatusCode);

        var fresh = await CreatePlayerInvite(coach, team.Id, roster.Id);
        var first = await player.PostAsJsonAsync("/player-invites/redeem", new { code = fresh.Code });
        first.EnsureSuccessStatusCode();
        var second = await other.PostAsJsonAsync("/player-invites/redeem", new { code = fresh.Code });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Redeem_is_idempotent_for_same_user()
    {
        var coach = await AuthenticatedClient("coach-id");
        var player = await AuthenticatedClient("player-id");
        var team = await CreateTeam(coach, "Bears SS", "bears-ss");
        var roster = await AddPlayer(coach, team.Id, "Idem Potent");
        var invite = await CreatePlayerInvite(coach, team.Id, roster.Id);

        var first = await player.PostAsJsonAsync("/player-invites/redeem", new { code = invite.Code });
        first.EnsureSuccessStatusCode();

        // Same user replays the same code — succeeds without erroring.
        var second = await player.PostAsJsonAsync("/player-invites/redeem", new { code = invite.Code });
        second.EnsureSuccessStatusCode();

        var mine = await player.GetFromJsonAsync<List<MyLinkedPlayerDto>>("/me/players");
        Assert.Single(mine!);
    }

    [Fact]
    public async Task Unlinked_user_cannot_view_or_submit_via_me()
    {
        var coach = await AuthenticatedClient("coach-x");
        var stranger = await AuthenticatedClient("stranger-x");
        var team = await CreateTeam(coach, "Pumas SS", "pumas-ss");
        var roster = await AddPlayer(coach, team.Id, "Walled Off");

        var list = await stranger.GetAsync($"/me/players/{roster.Id}/checkins");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

        var post = await stranger.PostAsJsonAsync(
            $"/me/players/{roster.Id}/checkins",
            new { sleepHours = 8.0, sorenessScore = 1, moodScore = 5, stressScore = 1, fatigueScore = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);

        var mine = await stranger.GetFromJsonAsync<List<MyLinkedPlayerDto>>("/me/players");
        Assert.Empty(mine!);
    }

    [Fact]
    public async Task Non_member_cannot_create_player_invite()
    {
        var coach = await AuthenticatedClient("coach-pi");
        var stranger = await AuthenticatedClient("stranger-pi");
        var team = await CreateTeam(coach, "Wolves SS", "wolves-ss");
        var roster = await AddPlayer(coach, team.Id, "Fenced");

        var resp = await stranger.PostAsync(
            $"/teams/{team.Id}/players/{roster.Id}/invites", null);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
