using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ForgeRise.Api.Auth;

public sealed record JwtOptions
{
    public required string Key { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);
}

public sealed record IssuedTokens(string AccessToken, DateTimeOffset AccessExpiresAt, string RefreshToken, DateTimeOffset RefreshExpiresAt);

public interface ITokenService
{
    Task<IssuedTokens> IssueAsync(User user, string? createdByIp, CancellationToken ct);
    Task<(IssuedTokens? Tokens, RefreshFailureReason? Failure)> RotateAsync(string presentedRefresh, string? createdByIp, CancellationToken ct);
    Task RevokeAsync(string presentedRefresh, CancellationToken ct);
}

public enum RefreshFailureReason { Missing, Unknown, Expired, Revoked, ReplayDetected }

public sealed class TokenService : ITokenService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _opts;
    private readonly TimeProvider _time;

    public TokenService(AppDbContext db, JwtOptions opts, TimeProvider time)
    {
        _db = db;
        _opts = opts;
        _time = time;
    }

    public async Task<IssuedTokens> IssueAsync(User user, string? createdByIp, CancellationToken ct)
    {
        var (refresh, hash) = NewRefreshToken();
        var refreshExpires = _time.GetUtcNow() + _opts.RefreshTokenLifetime;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = refreshExpires,
            CreatedAt = _time.GetUtcNow(),
            CreatedByIp = createdByIp,
        });
        await _db.SaveChangesAsync(ct);

        var (access, accessExpires) = MintAccessToken(user);
        return new IssuedTokens(access, accessExpires, refresh, refreshExpires);
    }

    public async Task<(IssuedTokens? Tokens, RefreshFailureReason? Failure)> RotateAsync(string presentedRefresh, string? createdByIp, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(presentedRefresh))
            return (null, RefreshFailureReason.Missing);

        var hash = HashToken(presentedRefresh);
        var stored = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

        if (stored is null) return (null, RefreshFailureReason.Unknown);

        if (stored.RevokedAt is not null)
        {
            // Replay of a revoked token: revoke entire descendant chain — defence in depth.
            await RevokeChainAsync(stored, ct);
            return (null, RefreshFailureReason.ReplayDetected);
        }

        if (_time.GetUtcNow() >= stored.ExpiresAt)
            return (null, RefreshFailureReason.Expired);

        // Rotate.
        var (refresh, newHash) = NewRefreshToken();
        var refreshExpires = _time.GetUtcNow() + _opts.RefreshTokenLifetime;

        var replacement = new RefreshToken
        {
            UserId = stored.UserId,
            TokenHash = newHash,
            ExpiresAt = refreshExpires,
            CreatedAt = _time.GetUtcNow(),
            CreatedByIp = createdByIp,
        };
        _db.RefreshTokens.Add(replacement);

        stored.RevokedAt = _time.GetUtcNow();
        stored.ReplacedByTokenId = replacement.Id;
        await _db.SaveChangesAsync(ct);

        var (access, accessExpires) = MintAccessToken(stored.User);
        return (new IssuedTokens(access, accessExpires, refresh, refreshExpires), null);
    }

    public async Task RevokeAsync(string presentedRefresh, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(presentedRefresh)) return;
        var hash = HashToken(presentedRefresh);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAt is not null) return;
        stored.RevokedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);
    }

    private async Task RevokeChainAsync(RefreshToken root, CancellationToken ct)
    {
        var current = root;
        while (current.ReplacedByTokenId is { } nextId)
        {
            var next = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Id == nextId, ct);
            if (next is null) break;
            if (next.RevokedAt is null) next.RevokedAt = _time.GetUtcNow();
            current = next;
        }
        await _db.SaveChangesAsync(ct);
    }

    private (string Token, DateTimeOffset ExpiresAt) MintAccessToken(User user)
    {
        var now = _time.GetUtcNow();
        var expires = now + _opts.AccessTokenLifetime;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("n")),
                new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            },
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private static (string Token, string Hash) NewRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Base64UrlEncoder.Encode(bytes);
        return (token, HashToken(token));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
