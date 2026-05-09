using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ForgeRise.Api.Tests.Auth;

public class TokenServiceTests
{
    private static (AppDbContext Db, TokenService Service, FakeTimeProvider Time, User User) Build()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tokens-{Guid.NewGuid():n}")
            .Options;
        var db = new AppDbContext(options);

        var user = new User
        {
            Email = "coach@example.com",
            DisplayName = "Coach",
            PasswordHash = "n/a",
        };
        db.Users.Add(user);
        db.SaveChanges();

        var jwt = new JwtOptions
        {
            Key = "test-jwt-key-must-be-at-least-32-chars-long-12345",
            Issuer = "forgerise.tests",
            Audience = "forgerise.tests",
        };
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        return (db, new TokenService(db, jwt, time), time, user);
    }

    [Fact]
    public async Task Issue_creates_a_persisted_refresh_record()
    {
        var (db, svc, _, user) = Build();

        var tokens = await svc.IssueAsync(user, "127.0.0.1", default);

        Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
        Assert.Equal(1, await db.RefreshTokens.CountAsync());
        var stored = await db.RefreshTokens.SingleAsync();
        Assert.NotEqual(tokens.RefreshToken, stored.TokenHash); // hashed only
    }

    [Fact]
    public async Task Rotate_revokes_old_and_issues_new()
    {
        var (db, svc, _, user) = Build();
        var first = await svc.IssueAsync(user, "ip", default);

        var (second, failure) = await svc.RotateAsync(first.RefreshToken, "ip", default);

        Assert.Null(failure);
        Assert.NotNull(second);
        Assert.NotEqual(first.RefreshToken, second!.RefreshToken);

        var rows = await db.RefreshTokens.AsNoTracking().ToListAsync();
        Assert.Equal(2, rows.Count);
        var revoked = rows.Single(r => r.RevokedAt is not null);
        var active = rows.Single(r => r.RevokedAt is null);
        Assert.Equal(active.Id, revoked.ReplacedByTokenId);
    }

    [Fact]
    public async Task Replay_of_revoked_token_is_rejected_and_revokes_chain()
    {
        var (db, svc, _, user) = Build();
        var first = await svc.IssueAsync(user, "ip", default);
        var (second, _) = await svc.RotateAsync(first.RefreshToken, "ip", default);
        Assert.NotNull(second);

        // Attacker re-uses the original (now-revoked) refresh token.
        var (third, failure) = await svc.RotateAsync(first.RefreshToken, "ip", default);

        Assert.Null(third);
        Assert.Equal(RefreshFailureReason.ReplayDetected, failure);

        // Chain revoked: every refresh row now revoked.
        var rows = await db.RefreshTokens.AsNoTracking().ToListAsync();
        Assert.All(rows, r => Assert.NotNull(r.RevokedAt));
    }

    [Fact]
    public async Task Expired_refresh_is_rejected()
    {
        var (_, svc, time, user) = Build();
        var first = await svc.IssueAsync(user, "ip", default);

        time.Advance(TimeSpan.FromDays(31));

        var (tokens, failure) = await svc.RotateAsync(first.RefreshToken, "ip", default);
        Assert.Null(tokens);
        Assert.Equal(RefreshFailureReason.Expired, failure);
    }

    [Fact]
    public async Task Unknown_refresh_returns_unknown_failure()
    {
        var (_, svc, _, _) = Build();
        var (tokens, failure) = await svc.RotateAsync("not-a-real-token", "ip", default);
        Assert.Null(tokens);
        Assert.Equal(RefreshFailureReason.Unknown, failure);
    }

    [Fact]
    public async Task Revoke_marks_token_revoked()
    {
        var (db, svc, _, user) = Build();
        var first = await svc.IssueAsync(user, "ip", default);

        await svc.RevokeAsync(first.RefreshToken, default);

        var stored = await db.RefreshTokens.AsNoTracking().SingleAsync();
        Assert.NotNull(stored.RevokedAt);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset start) => _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan span) => _now = _now.Add(span);
    }
}
