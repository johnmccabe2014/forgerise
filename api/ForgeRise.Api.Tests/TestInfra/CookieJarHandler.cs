using ForgeRise.Api.Auth;

namespace ForgeRise.Api.Tests.TestInfra;

/// <summary>
/// Cookie-jar handler that also auto-attaches the X-CSRF-Token header on
/// unsafe verbs once a session cookie has been set, so test code can post
/// to protected endpoints without manual cookie plumbing.
/// </summary>
public sealed class CookieJarHandler : DelegatingHandler
{
    public System.Net.CookieContainer Jar { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? new Uri("http://localhost/");

        var cookieHeader = Jar.GetCookieHeader(uri);
        if (!string.IsNullOrEmpty(cookieHeader))
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

        var method = request.Method.Method;
        var safe = method is "GET" or "HEAD" or "OPTIONS" or "TRACE";
        var path = uri.AbsolutePath;
        var exempt = path is "/auth/register" or "/auth/login" or "/auth/refresh" or "/auth/logout";
        if (!safe && !exempt)
        {
            var csrf = Jar.GetCookies(uri)[AuthCookies.CsrfCookie]?.Value;
            if (!string.IsNullOrEmpty(csrf))
                request.Headers.TryAddWithoutValidation(AuthCookies.CsrfHeader, csrf);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            foreach (var sc in setCookies) Jar.SetCookies(uri, sc);
        return response;
    }
}
