using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Auth;
using ForgeRise.Api.Tests.TestInfra;
using Xunit;

namespace ForgeRise.Api.Tests.Security;

public class CsrfMiddlewareTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public CsrfMiddlewareTests(ForgeRiseFactory factory) => _factory = factory;

    [Fact]
    public async Task Cookie_authenticated_post_without_csrf_header_is_403()
    {
        // Step 1: register via the cookie-jar client to get a session.
        var jar = new System.Net.CookieContainer();
        var client = _factory.CreateDefaultClient(new ForwardingHandler(jar));

        var register = await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"csrf-{Guid.NewGuid():n}@example.com",
            password = "Correct horse battery staple",
            displayName = "csrf",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        // Step 2: hit a protected endpoint with the cookie but WITHOUT the CSRF header.
        var attack = new HttpRequestMessage(HttpMethod.Post, "/teams")
        {
            Content = JsonContent.Create(new { name = "Pirates", code = "pirates" }),
        };
        var uri = new Uri("http://localhost/");
        attack.Headers.TryAddWithoutValidation("Cookie", jar.GetCookieHeader(uri));

        var raw = _factory.CreateDefaultClient();
        var resp = await raw.SendAsync(attack);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    private sealed class ForwardingHandler : DelegatingHandler
    {
        private readonly System.Net.CookieContainer _jar;
        public ForwardingHandler(System.Net.CookieContainer jar) => _jar = jar;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? new Uri("http://localhost/");
            var cookieHeader = _jar.GetCookieHeader(uri);
            if (!string.IsNullOrEmpty(cookieHeader))
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

            var resp = await base.SendAsync(request, cancellationToken);
            if (resp.Headers.TryGetValues("Set-Cookie", out var setCookies))
                foreach (var sc in setCookies) _jar.SetCookies(uri, sc);
            return resp;
        }
    }
}
