using Microsoft.AspNetCore.Http;

namespace ForgeRise.Api.Auth;

public static class AuthCookies
{
    public const string AccessTokenCookie = "fr_at";
    public const string RefreshTokenCookie = "fr_rt";
    public const string CsrfCookie = "fr_csrf";
    public const string CsrfHeader = "X-CSRF-Token";

    public static void SetTokens(HttpResponse response, IssuedTokens tokens, bool secure)
    {
        response.Cookies.Append(AccessTokenCookie, tokens.AccessToken, BaseOptions(tokens.AccessExpiresAt, secure, httpOnly: true));
        response.Cookies.Append(RefreshTokenCookie, tokens.RefreshToken, BaseOptions(tokens.RefreshExpiresAt, secure, httpOnly: true, path: "/auth"));
    }

    public static void SetCsrf(HttpResponse response, string token, bool secure, DateTimeOffset expiresAt)
    {
        response.Cookies.Append(CsrfCookie, token, BaseOptions(expiresAt, secure, httpOnly: false));
    }

    public static void Clear(HttpResponse response, bool secure)
    {
        var past = DateTimeOffset.UtcNow.AddDays(-1);
        response.Cookies.Append(AccessTokenCookie, string.Empty, BaseOptions(past, secure, httpOnly: true));
        response.Cookies.Append(RefreshTokenCookie, string.Empty, BaseOptions(past, secure, httpOnly: true, path: "/auth"));
        response.Cookies.Append(CsrfCookie, string.Empty, BaseOptions(past, secure, httpOnly: false));
    }

    private static CookieOptions BaseOptions(DateTimeOffset expires, bool secure, bool httpOnly, string path = "/")
        => new()
        {
            HttpOnly = httpOnly,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = path,
            Expires = expires,
            IsEssential = true,
        };
}
