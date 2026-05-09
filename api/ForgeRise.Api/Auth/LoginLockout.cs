using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Auth;

/// <summary>
/// Account lockout: 10 failed logins within a 15-minute window ⇒ 15-minute lockout.
/// Persisted on the user row so it survives a process restart. Replace with a
/// distributed counter (Redis) when we scale beyond a single API replica.
/// </summary>
public interface ILoginLockout
{
    bool IsLockedOut(User user);
    Task RecordFailureAsync(User user, CancellationToken ct);
    Task RecordSuccessAsync(User user, CancellationToken ct);
}

public sealed class LoginLockout : ILoginLockout
{
    public const int FailureThreshold = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public LoginLockout(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public bool IsLockedOut(User user) =>
        user.LockoutUntil is { } until && _time.GetUtcNow() < until;

    public async Task RecordFailureAsync(User user, CancellationToken ct)
    {
        user.FailedLoginCount += 1;
        if (user.FailedLoginCount >= FailureThreshold)
        {
            user.LockoutUntil = _time.GetUtcNow() + LockoutDuration;
            user.FailedLoginCount = 0;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordSuccessAsync(User user, CancellationToken ct)
    {
        if (user.FailedLoginCount == 0 && user.LockoutUntil is null) return;
        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        await _db.SaveChangesAsync(ct);
    }
}
