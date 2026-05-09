using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ForgeRise.Api.Auth;
using ForgeRise.Api.Tests.TestInfra;
using ForgeRise.Api.Teams.Contracts;
using Xunit;

namespace ForgeRise.Api.Tests.Teams;

/// <summary>
/// Multi-coach membership: invite codes, join flow, role-gated endpoints.
/// Pairs with <see cref="TeamsAndPlayersTests"/> which still asserts the
/// solo-owner happy path.
/// </summary>
public class MultiCoachTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public MultiCoachTests(ForgeRiseFactory factory) => _factory = factory;

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

    private static async Task<TeamInviteDto> CreateInvite(HttpClient client, Guid teamId)
    {
        var resp = await client.PostAsync($"/teams/{teamId}/invites", content: null);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<TeamInviteDto>())!;
    }

    [Fact]
    public async Task Owner_can_create_invite_and_coach_can_join()
    {
        var owner = await AuthenticatedClient("owner");
        var coach = await AuthenticatedClient("coach");

        var team = await CreateTeam(owner, "Lions", "lions-mc");
        Assert.Equal("owner", team.MyRole);
        Assert.Equal(1, team.CoachCount);

        var invite = await CreateInvite(owner, team.Id);
        Assert.False(string.IsNullOrWhiteSpace(invite.Code));
        Assert.True(invite.ExpiresAt > DateTimeOffset.UtcNow);

        // Coach joins.
        var joinResp = await coach.PostAsJsonAsync("/teams/join", new { code = invite.Code });
        joinResp.EnsureSuccessStatusCode();
        var joined = (await joinResp.Content.ReadFromJsonAsync<TeamDto>())!;
        Assert.Equal(team.Id, joined.Id);
        Assert.Equal("coach", joined.MyRole);
        Assert.Equal(2, joined.CoachCount);

        // Coach now sees the team in their list.
        var listed = await coach.GetFromJsonAsync<List<TeamDto>>("/teams");
        Assert.Single(listed!);
        Assert.Equal("coach", listed![0].MyRole);

        // Coach can list+add players (write access).
        var addPlayer = await coach.PostAsJsonAsync($"/teams/{team.Id}/players",
            new { displayName = "Joined Player", jerseyNumber = 9, position = "FH" });
        Assert.Equal(HttpStatusCode.Created, addPlayer.StatusCode);
        var players = await coach.GetFromJsonAsync<List<PlayerDto>>($"/teams/{team.Id}/players");
        Assert.Single(players!);
    }

    [Fact]
    public async Task Coach_cannot_delete_team_or_manage_invites()
    {
        var owner = await AuthenticatedClient("owner-d");
        var coach = await AuthenticatedClient("coach-d");
        var team = await CreateTeam(owner, "Tigers", "tigers-mc");
        var invite = await CreateInvite(owner, team.Id);
        await coach.PostAsJsonAsync("/teams/join", new { code = invite.Code });

        var del = await coach.DeleteAsync($"/teams/{team.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);

        var newInvite = await coach.PostAsync($"/teams/{team.Id}/invites", null);
        Assert.Equal(HttpStatusCode.Forbidden, newInvite.StatusCode);

        var listInvites = await coach.GetAsync($"/teams/{team.Id}/invites");
        Assert.Equal(HttpStatusCode.Forbidden, listInvites.StatusCode);
    }

    [Fact]
    public async Task Non_member_cannot_create_invite_or_see_coaches()
    {
        var owner = await AuthenticatedClient("o2");
        var stranger = await AuthenticatedClient("s2");
        var team = await CreateTeam(owner, "Bears", "bears-mc");

        var inv = await stranger.PostAsync($"/teams/{team.Id}/invites", null);
        Assert.Equal(HttpStatusCode.Forbidden, inv.StatusCode);

        var coaches = await stranger.GetAsync($"/teams/{team.Id}/coaches");
        Assert.Equal(HttpStatusCode.Forbidden, coaches.StatusCode);
    }

    [Fact]
    public async Task Invite_consumed_once_then_rejected()
    {
        var owner = await AuthenticatedClient("o3");
        var coachA = await AuthenticatedClient("ca");
        var coachB = await AuthenticatedClient("cb");
        var team = await CreateTeam(owner, "Pumas", "pumas-mc");
        var invite = await CreateInvite(owner, team.Id);

        var first = await coachA.PostAsJsonAsync("/teams/join", new { code = invite.Code });
        first.EnsureSuccessStatusCode();

        var second = await coachB.PostAsJsonAsync("/teams/join", new { code = invite.Code });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invite_consumed", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Invite_revoked_cannot_be_used()
    {
        var owner = await AuthenticatedClient("o4");
        var coach = await AuthenticatedClient("c4");
        var team = await CreateTeam(owner, "Sharks", "sharks-mc");
        var invite = await CreateInvite(owner, team.Id);

        var revoke = await owner.DeleteAsync($"/teams/{team.Id}/invites/{invite.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var join = await coach.PostAsJsonAsync("/teams/join", new { code = invite.Code });
        Assert.Equal(HttpStatusCode.Conflict, join.StatusCode);
    }

    [Fact]
    public async Task Unknown_invite_code_returns_404()
    {
        var coach = await AuthenticatedClient("c5");
        var join = await coach.PostAsJsonAsync("/teams/join", new { code = "no-such-code-xxx" });
        Assert.Equal(HttpStatusCode.NotFound, join.StatusCode);
    }

    [Fact]
    public async Task Owner_can_remove_coach_but_not_themselves()
    {
        var owner = await AuthenticatedClient("o6");
        var coach = await AuthenticatedClient("c6");
        var team = await CreateTeam(owner, "Wasps", "wasps-mc");
        var invite = await CreateInvite(owner, team.Id);
        var joined = await coach.PostAsJsonAsync("/teams/join", new { code = invite.Code });
        joined.EnsureSuccessStatusCode();

        var coaches = await owner.GetFromJsonAsync<List<TeamCoachDto>>($"/teams/{team.Id}/coaches");
        Assert.Equal(2, coaches!.Count);
        var coachRow = coaches.Single(c => c.Role == "coach");
        var ownerRow = coaches.Single(c => c.Role == "owner");

        var removeCoach = await owner.DeleteAsync($"/teams/{team.Id}/coaches/{coachRow.UserId}");
        Assert.Equal(HttpStatusCode.NoContent, removeCoach.StatusCode);

        var removeSelf = await owner.DeleteAsync($"/teams/{team.Id}/coaches/{ownerRow.UserId}");
        Assert.Equal(HttpStatusCode.Conflict, removeSelf.StatusCode);
    }

    [Fact]
    public async Task Owner_can_transfer_ownership_to_existing_coach()
    {
        var owner = await AuthenticatedClient("o7");
        var coach = await AuthenticatedClient("c7");
        var team = await CreateTeam(owner, "Eagles", "eagles-mc");
        var invite = await CreateInvite(owner, team.Id);
        (await coach.PostAsJsonAsync("/teams/join", new { code = invite.Code })).EnsureSuccessStatusCode();

        var coaches = await owner.GetFromJsonAsync<List<TeamCoachDto>>($"/teams/{team.Id}/coaches");
        var coachRow = coaches!.Single(c => c.Role == "coach");

        var transfer = await owner.PostAsync(
            $"/teams/{team.Id}/coaches/{coachRow.UserId}/transfer-ownership", content: null);
        Assert.Equal(HttpStatusCode.NoContent, transfer.StatusCode);

        // Roles flipped.
        var afterOwner = await owner.GetFromJsonAsync<TeamDto>($"/teams/{team.Id}");
        Assert.Equal("coach", afterOwner!.MyRole);
        var afterCoach = await coach.GetFromJsonAsync<TeamDto>($"/teams/{team.Id}");
        Assert.Equal("owner", afterCoach!.MyRole);

        // Original owner can no longer create invites; new owner can.
        var oldOwnerInvite = await owner.PostAsync($"/teams/{team.Id}/invites", null);
        Assert.Equal(HttpStatusCode.Forbidden, oldOwnerInvite.StatusCode);
        var newOwnerInvite = await coach.PostAsync($"/teams/{team.Id}/invites", null);
        Assert.Equal(HttpStatusCode.Created, newOwnerInvite.StatusCode);

        // Team still has exactly one Owner.
        var rosterAfter = await coach.GetFromJsonAsync<List<TeamCoachDto>>($"/teams/{team.Id}/coaches");
        Assert.Single(rosterAfter!, c => c.Role == "owner");
    }

    [Fact]
    public async Task Non_owner_cannot_transfer_ownership()
    {
        var owner = await AuthenticatedClient("o8");
        var coach = await AuthenticatedClient("c8");
        var team = await CreateTeam(owner, "Falcons", "falcons-mc");
        var invite = await CreateInvite(owner, team.Id);
        (await coach.PostAsJsonAsync("/teams/join", new { code = invite.Code })).EnsureSuccessStatusCode();

        var roster = await owner.GetFromJsonAsync<List<TeamCoachDto>>($"/teams/{team.Id}/coaches");
        var ownerRow = roster!.Single(c => c.Role == "owner");

        // Coach trying to grab ownership for themselves → 403.
        var grab = await coach.PostAsync(
            $"/teams/{team.Id}/coaches/{ownerRow.UserId}/transfer-ownership", null);
        Assert.Equal(HttpStatusCode.Forbidden, grab.StatusCode);
    }

    [Fact]
    public async Task Transfer_to_non_member_returns_404()
    {
        var owner = await AuthenticatedClient("o9");
        var stranger = await AuthenticatedClient("st9");
        var team = await CreateTeam(owner, "Hawks", "hawks-mc");

        // Get stranger's user id by having them register and read /me.
        var me = await stranger.GetFromJsonAsync<JsonElement>("/auth/me");
        var strangerId = me.GetProperty("id").GetGuid();

        var transfer = await owner.PostAsync(
            $"/teams/{team.Id}/coaches/{strangerId}/transfer-ownership", null);
        Assert.Equal(HttpStatusCode.NotFound, transfer.StatusCode);
    }

    [Fact]
    public async Task Transfer_to_self_is_noop()
    {
        var owner = await AuthenticatedClient("o10");
        var team = await CreateTeam(owner, "Ospreys", "ospreys-mc");
        var roster = await owner.GetFromJsonAsync<List<TeamCoachDto>>($"/teams/{team.Id}/coaches");
        var me = roster!.Single().UserId;

        var transfer = await owner.PostAsync(
            $"/teams/{team.Id}/coaches/{me}/transfer-ownership", null);
        Assert.Equal(HttpStatusCode.NoContent, transfer.StatusCode);

        var after = await owner.GetFromJsonAsync<TeamDto>($"/teams/{team.Id}");
        Assert.Equal("owner", after!.MyRole);
    }

    private sealed class CookieJarHandler : DelegatingHandler
    {
        private readonly System.Net.CookieContainer _jar = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? new Uri("http://localhost/");
            var cookieHeader = _jar.GetCookieHeader(uri);
            if (!string.IsNullOrEmpty(cookieHeader))
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

            var method = request.Method.Method;
            var safe = method is "GET" or "HEAD" or "OPTIONS" or "TRACE";
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var exempt = path is "/auth/register" or "/auth/login" or "/auth/refresh" or "/auth/logout";
            if (!safe && !exempt)
            {
                var csrf = _jar.GetCookies(uri)[AuthCookies.CsrfCookie]?.Value;
                if (!string.IsNullOrEmpty(csrf))
                    request.Headers.TryAddWithoutValidation(AuthCookies.CsrfHeader, csrf);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var sc in setCookies) _jar.SetCookies(uri, sc);
            }
            return response;
        }
    }
}
