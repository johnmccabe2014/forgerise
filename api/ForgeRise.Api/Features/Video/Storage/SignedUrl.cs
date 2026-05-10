using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ForgeRise.Api.Features.Video.Storage;

/// <summary>
/// Static helpers that mint and verify the V2 signed-URL contract:
/// <c>?exp={unix}&amp;v={viewerUserId}&amp;sig={hex(HMAC-SHA256)}</c>.
///
/// The HMAC binds three parameters together so a leaked URL cannot be replayed
/// for a different viewer or after expiry (security review iter1, finding #4):
///
/// <code>
/// sig = HMAC-SHA256(secret, $"{storagePath}|{viewerUserId:N}|{expUnixSeconds}")
/// </code>
/// </summary>
internal static class SignedUrl
{
    /// <summary>Computes the HMAC-SHA256 for the canonical message and returns lower-case hex.</summary>
    public static string Sign(string storagePath, Guid viewerUserId, long expUnixSeconds, byte[] secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(storagePath);
        if (secret.Length < 32)
        {
            throw new InvalidOperationException("Video signing secret must be >= 32 bytes.");
        }

        var msg = $"{storagePath}|{viewerUserId:N}|{expUnixSeconds.ToString(CultureInfo.InvariantCulture)}";
        var bytes = Encoding.UTF8.GetBytes(msg);
        var mac = HMACSHA256.HashData(secret, bytes);
        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    /// <summary>
    /// Constant-time verification. Returns <c>true</c> iff <paramref name="sig"/>
    /// matches the canonical HMAC for (path, viewer, exp) AND <paramref name="exp"/>
    /// has not yet elapsed against <paramref name="now"/>.
    /// </summary>
    public static bool Verify(
        string storagePath,
        Guid viewerUserId,
        long expUnixSeconds,
        string sig,
        byte[] secret,
        DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(sig)) return false;
        if (DateTimeOffset.FromUnixTimeSeconds(expUnixSeconds) <= now) return false;

        var expected = Sign(storagePath, viewerUserId, expUnixSeconds, secret);
        var a = Encoding.ASCII.GetBytes(expected);
        var b = Encoding.ASCII.GetBytes(sig);
        if (a.Length != b.Length) return false;
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
