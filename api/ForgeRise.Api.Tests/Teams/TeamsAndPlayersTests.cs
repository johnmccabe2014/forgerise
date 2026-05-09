using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Auth;
using ForgeRise.Api.Tests.TestInfra;
using ForgeRise.Api.Teams.Contracts;
using Xunit;

namespace ForgeRise.Api.Tests.Teams;

public class TeamsAndPlayersTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public TeamsAndPlayersTests(ForgeRiseFactory factory) => _factory = factory;

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

    [Fact]
    public async Task Anonymous_cannot_list_teams()
    {
        var client = _factory.CreateDefaultClient();
        var resp = await client.GetAsync("/teams");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Owner_can_create_list_get_update_delete_team()
    {
        var client = await AuthenticatedClient("alice");

        var create = await client.PostAsJsonAsync("/teams", new { name = "Lions U10", code = "lions-u10" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var team = await create.Content.ReadFromJsonAsync<TeamDto>();
        Assert.NotNull(team);
        Assert.Equal("Lions U10", team!.Name);

        var list = await client.GetFromJsonAsync<List<TeamDto>>("/teams");
        Assert.NotNull(list);
        Assert.Single(list!);

        var get = await client.GetFromJsonAsync<TeamDto>($"/teams/{team.Id}");
        Assert.Equal(team.Id, get!.Id);

        var update = await client.PutAsJsonAsync($"/teams/{team.Id}", new { name = "Lions U11" });
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<TeamDto>();
        Assert.Equal("Lions U11", updated!.Name);

        var del = await client.DeleteAsync($"/teams/{team.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var listAfter = await client.GetFromJsonAsync<List<TeamDto>>("/teams");
        Assert.Empty(listAfter!);
    }

    [Fact]
    public async Task Cannot_create_two_teams_with_same_code_for_same_owner()
    {
        var client = await AuthenticatedClient("bob");

        var first = await client.PostAsJsonAsync("/teams", new { name = "Tigers", code = "tigers" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/teams", new { name = "Tigers Again", code = "tigers" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Other_user_cannot_access_team()
    {
        var owner = await AuthenticatedClient("carol");
        var stranger = await AuthenticatedClient("dave");

        var create = await owner.PostAsJsonAsync("/teams", new { name = "Bears", code = "bears" });
        var team = await create.Content.ReadFromJsonAsync<TeamDto>();

        var ownerListResp = await stranger.GetFromJsonAsync<List<TeamDto>>("/teams");
        Assert.Empty(ownerListResp!);

        var get = await stranger.GetAsync($"/teams/{team!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);

        var update = await stranger.PutAsJsonAsync($"/teams/{team.Id}", new { name = "Hijacked" });
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);

        var del = await stranger.DeleteAsync($"/teams/{team.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);
    }

    [Fact]
    public async Task Player_crud_for_owner()
    {
        var client = await AuthenticatedClient("erin");
        var t = await client.PostAsJsonAsync("/teams", new { name = "Wolves", code = "wolves" });
        var team = await t.Content.ReadFromJsonAsync<TeamDto>();

        var create = await client.PostAsJsonAsync($"/teams/{team!.Id}/players",
            new { displayName = "Sam Smith", jerseyNumber = 7, birthYear = 2014, position = "MID" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var player = await create.Content.ReadFromJsonAsync<PlayerDto>();

        var list = await client.GetFromJsonAsync<List<PlayerDto>>($"/teams/{team.Id}/players");
        Assert.Single(list!);

        var update = await client.PutAsJsonAsync($"/teams/{team.Id}/players/{player!.Id}",
            new { displayName = "Samuel Smith", jerseyNumber = 8, birthYear = 2014, position = "FWD", isActive = true });
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<PlayerDto>();
        Assert.Equal("Samuel Smith", updated!.DisplayName);
        Assert.Equal(8, updated.JerseyNumber);

        var del = await client.DeleteAsync($"/teams/{team.Id}/players/{player.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var listAfter = await client.GetFromJsonAsync<List<PlayerDto>>($"/teams/{team.Id}/players");
        Assert.Empty(listAfter!);
    }

    [Fact]
    public async Task Player_endpoints_reject_non_owner()
    {
        var owner = await AuthenticatedClient("frank");
        var stranger = await AuthenticatedClient("grace");

        var t = await owner.PostAsJsonAsync("/teams", new { name = "Hawks", code = "hawks" });
        var team = await t.Content.ReadFromJsonAsync<TeamDto>();

        var resp = await stranger.PostAsJsonAsync($"/teams/{team!.Id}/players",
            new { displayName = "Mallory" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);

        var list = await stranger.GetAsync($"/teams/{team.Id}/players");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
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

            // CSRF: our middleware enforces double-submit on cookie-authenticated unsafe verbs.
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
