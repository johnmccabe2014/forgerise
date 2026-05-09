using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Auth;
using ForgeRise.Api.Auth.Contracts;
using ForgeRise.Api.Tests.TestInfra;
using Xunit;

namespace ForgeRise.Api.Tests.Auth;

public class AuthEndpointsTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public AuthEndpointsTests(ForgeRiseFactory factory) => _factory = factory;

    private HttpClient NewClient() => _factory.CreateDefaultClient(new HandlerWithCookies());

    [Fact]
    public async Task Register_login_me_logout_happy_path()
    {
        var client = NewClient();

        var register = await client.PostAsJsonAsync("/auth/register", new
        {
            email = "alice@example.com",
            password = "Correct horse battery staple",
            displayName = "Alice",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var me = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var dto = await me.Content.ReadFromJsonAsync<AuthUserDto>();
        Assert.NotNull(dto);
        Assert.Equal("alice@example.com", dto!.Email);

        var logout = await client.PostAsync("/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var meAfter = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_short_password()
    {
        var client = NewClient();
        var resp = await client.PostAsJsonAsync("/auth/register", new
        {
            email = "bob@example.com",
            password = "tooshort",
            displayName = "Bob",
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        var client = NewClient();
        var payload = new { email = "carol@example.com", password = "Correct horse battery staple", displayName = "Carol" };

        var first = await client.PostAsJsonAsync("/auth/register", payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/auth/register", payload);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_with_generic_error()
    {
        var client = NewClient();
        await client.PostAsJsonAsync("/auth/register", new
        {
            email = "dave@example.com",
            password = "Correct horse battery staple",
            displayName = "Dave",
        });

        var login = await client.PostAsJsonAsync("/auth/login", new
        {
            email = "dave@example.com",
            password = "definitely-the-wrong-password",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_and_returns_new_cookies()
    {
        var client = NewClient();

        var register = await client.PostAsJsonAsync("/auth/register", new
        {
            email = "erin@example.com",
            password = "Correct horse battery staple",
            displayName = "Erin",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var refresh = await client.PostAsync("/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var me = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Refresh_replay_revokes_session()
    {
        // Use two clients that share state via raw cookie capture.
        var first = NewClient();
        var registerResp = await first.PostAsJsonAsync("/auth/register", new
        {
            email = "frank@example.com",
            password = "Correct horse battery staple",
            displayName = "Frank",
        });
        Assert.Equal(HttpStatusCode.Created, registerResp.StatusCode);

        var stolenRefresh = ExtractCookie(registerResp, AuthCookies.RefreshTokenCookie);
        Assert.NotNull(stolenRefresh);

        // Legitimate user rotates.
        var rotateResp = await first.PostAsync("/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, rotateResp.StatusCode);

        // Attacker replays the original refresh on a fresh client.
        var attacker = _factory.CreateDefaultClient();
        var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        replayRequest.Headers.Add("Cookie", $"{AuthCookies.RefreshTokenCookie}={stolenRefresh}");
        var replay = await attacker.SendAsync(replayRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The legitimate user's *new* refresh cookie should now also be invalid (chain revoked).
        var followUp = await first.PostAsync("/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, followUp.StatusCode);
    }

    private static string? ExtractCookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return null;
        foreach (var v in values)
        {
            var first = v.Split(';', 2)[0];
            var eq = first.IndexOf('=');
            if (eq > 0 && first[..eq] == name) return first[(eq + 1)..];
        }
        return null;
    }

    private sealed class HandlerWithCookies : DelegatingHandler
    {
        // Default handler inserted by CreateDefaultClient already includes a CookieContainer
        // when the inner cookie support is enabled — but since CreateDefaultClient strips it,
        // we add our own.
        private readonly System.Net.CookieContainer _jar = new();

        public HandlerWithCookies()
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? new Uri("http://localhost/");
            var cookieHeader = _jar.GetCookieHeader(uri);
            if (!string.IsNullOrEmpty(cookieHeader))
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

            var response = await base.SendAsync(request, cancellationToken);

            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var sc in setCookies) _jar.SetCookies(uri, sc);
            }
            return response;
        }
    }
}
