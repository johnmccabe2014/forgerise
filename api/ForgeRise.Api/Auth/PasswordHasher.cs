using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace ForgeRise.Api.Auth;

/// <summary>
/// Argon2id password hashing per OWASP 2024 guidance:
/// 19 MiB memory, 2 iterations, parallelism = 1, 16-byte salt, 32-byte hash.
/// Encoded format: argon2id$v=19$m=19456,t=2,p=1$&lt;salt-b64&gt;$&lt;hash-b64&gt;
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int MemoryKb = 19_456;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string Prefix = "argon2id$v=19";

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = ComputeHash(password, salt, MemoryKb, Iterations, Parallelism);
        return $"{Prefix}$m={MemoryKb},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public PasswordVerificationResult Verify(string password, string encoded)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(encoded))
            return PasswordVerificationResult.Failed;

        if (!TryParse(encoded, out var p))
            return PasswordVerificationResult.Failed;

        var computed = ComputeHash(password, p.Salt, p.MemoryKb, p.Iterations, p.Parallelism);
        if (!CryptographicOperations.FixedTimeEquals(computed, p.Hash))
            return PasswordVerificationResult.Failed;

        var needsRehash = p.MemoryKb != MemoryKb || p.Iterations != Iterations || p.Parallelism != Parallelism;
        return needsRehash ? PasswordVerificationResult.SuccessRehashNeeded : PasswordVerificationResult.Success;
    }

    private static byte[] ComputeHash(string password, byte[] salt, int memoryKb, int iterations, int parallelism)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            MemorySize = memoryKb,
            Iterations = iterations,
        };
        return argon.GetBytes(HashBytes);
    }

    private static bool TryParse(string encoded, out (int MemoryKb, int Iterations, int Parallelism, byte[] Salt, byte[] Hash) parsed)
    {
        parsed = default;
        var parts = encoded.Split('$');
        if (parts.Length != 5 || parts[0] != "argon2id" || parts[1] != "v=19") return false;

        var paramSegments = parts[2].Split(',');
        if (paramSegments.Length != 3) return false;

        if (!TryParam(paramSegments[0], "m", out var m)) return false;
        if (!TryParam(paramSegments[1], "t", out var t)) return false;
        if (!TryParam(paramSegments[2], "p", out var p)) return false;

        try
        {
            var salt = Convert.FromBase64String(parts[3]);
            var hash = Convert.FromBase64String(parts[4]);
            parsed = (m, t, p, salt, hash);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryParam(string segment, string key, out int value)
    {
        value = 0;
        var kv = segment.Split('=');
        return kv.Length == 2 && kv[0] == key && int.TryParse(kv[1], out value);
    }
}

public interface IPasswordHasher
{
    string Hash(string password);
    PasswordVerificationResult Verify(string password, string encoded);
}

public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded,
}
