using ForgeRise.Api.Auth;

namespace ForgeRise.Api.Observability;

/// <summary>
/// Double-submit CSRF protection: when a request carries the access-token
/// cookie (browser session), unsafe verbs must include an X-CSRF-Token header
/// matching the fr_csrf cookie. API clients using Authorization: Bearer are
/// unaffected.
/// </summary>
public sealed class CsrfMiddleware
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS", "TRACE",
    };

    // Endpoints that establish a session and therefore can't have a CSRF cookie yet.
    private static readonly HashSet<string> Exempt = new(StringComparer.OrdinalIgnoreCase)
    {
        "/auth/register", "/auth/login", "/auth/refresh", "/auth/logout",
    };

    private readonly RequestDelegate _next;

    public CsrfMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (SafeMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (Exempt.Contains(path))
        {
            await _next(context);
            return;
        }

        // Only enforce when the caller is using cookie auth.
        var hasAccessCookie = context.Request.Cookies.ContainsKey(AuthCookies.AccessTokenCookie);
        if (!hasAccessCookie)
        {
            await _next(context);
            return;
        }

        var cookieToken = context.Request.Cookies[AuthCookies.CsrfCookie];
        var headerToken = context.Request.Headers[AuthCookies.CsrfHeader].ToString();

        if (string.IsNullOrEmpty(cookieToken) ||
            string.IsNullOrEmpty(headerToken) ||
            !CryptographicEquals(cookieToken, headerToken))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "csrf_failed" });
            return;
        }

        await _next(context);
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
