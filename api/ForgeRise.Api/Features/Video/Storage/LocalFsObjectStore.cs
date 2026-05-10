using System.Security.Cryptography;
using System.Text;
using ForgeRise.Api.Features.Video.Options;
using Microsoft.Extensions.Options;

namespace ForgeRise.Api.Features.Video.Storage;

/// <summary>
/// V2 implementation of <see cref="IObjectStore"/>. Persists bytes under
/// <see cref="VideoStorageOptions.Root"/> using a server-generated relative
/// key. Hard-blocks path traversal (security review iter1, finding F5) and
/// refuses to overwrite (uses <c>FileMode.CreateNew</c>).
///
/// Signed URLs are minted as relative paths
/// <c>/v1/videos/blob?path=…&amp;exp=…&amp;v=…&amp;sig=…</c>; the
/// <see cref="Endpoints.StreamController"/> verifies and serves them. Cloud
/// backends (S3, Azure Blob) will replace this class wholesale.
/// </summary>
public sealed class LocalFsObjectStore : IObjectStore
{
    private readonly VideoStorageOptions _storage;
    private readonly VideoSigningOptions _signing;
    private readonly TimeProvider _time;
    private readonly byte[] _secret;

    public LocalFsObjectStore(
        IOptions<VideoStorageOptions> storage,
        IOptions<VideoSigningOptions> signing,
        TimeProvider time)
    {
        _storage = storage.Value;
        _signing = signing.Value;
        _time = time;
        _secret = Encoding.UTF8.GetBytes(_signing.SigningSecret ?? string.Empty);
    }

    /// <inheritdoc/>
    public async Task<string> PutAsync(
        string suggestedRelativePath,
        Stream content,
        string contentType,
        CancellationToken ct)
    {
        var (root, full) = ResolveStrict(suggestedRelativePath);
        EnsureFreeSpace(root);

        var dir = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(dir);

        var tmpDir = Path.Combine(root, ".uploads-tmp");
        Directory.CreateDirectory(tmpDir);
        var tmp = Path.Combine(tmpDir, Guid.NewGuid().ToString("n"));

        try
        {
            await using (var fs = new FileStream(
                tmp,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await content.CopyToAsync(fs, ct);
                await fs.FlushAsync(ct);
            }

            // Move is atomic on the same filesystem; CreateNew semantics
            // come from the explicit existence check.
            if (File.Exists(full))
            {
                throw new IOException($"Storage path already exists: {suggestedRelativePath}");
            }
            File.Move(tmp, full);
            return suggestedRelativePath;
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<Uri> GetSignedReadUrlAsync(
        string storagePath,
        Guid viewerUserId,
        DateTimeOffset expiresAt,
        CancellationToken ct)
    {
        var ttl = expiresAt - _time.GetUtcNow();
        if (ttl <= TimeSpan.Zero || ttl > _signing.MaxUrlTtl)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAt),
                $"Signed URL TTL must be in (0, {_signing.MaxUrlTtl.TotalMinutes:F0}m].");
        }

        // Validate the path resolves under root, even though it isn't read here.
        ResolveStrict(storagePath);

        var exp = expiresAt.ToUnixTimeSeconds();
        var sig = SignedUrl.Sign(storagePath, viewerUserId, exp, _secret);
        var encoded = Uri.EscapeDataString(storagePath);
        var uri = new Uri(
            $"/v1/videos/blob?path={encoded}&v={viewerUserId:N}&exp={exp}&sig={sig}",
            UriKind.Relative);
        return Task.FromResult(uri);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string storagePath, CancellationToken ct)
    {
        var (_, full) = ResolveStrict(storagePath);
        if (File.Exists(full))
        {
            try { File.Delete(full); } catch (FileNotFoundException) { /* race-safe */ }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verify the bytes are below the storage root and return both the root
    /// and the absolute path. Public so the stream controller can read
    /// (read-side verification re-runs the same check).
    /// </summary>
    public (string root, string full) ResolveStrict(string relativePath)
    {
        if (string.IsNullOrEmpty(_storage.Root))
        {
            throw new InvalidOperationException("VideoStorageOptions.Root not configured.");
        }
        if (string.IsNullOrEmpty(relativePath))
        {
            throw new ArgumentException("Relative path is empty.", nameof(relativePath));
        }
        if (relativePath.Contains("..", StringComparison.Ordinal) ||
            relativePath.Contains('\\') ||
            relativePath.Contains(':') ||
            relativePath.Contains('\0') ||
            relativePath.StartsWith('/'))
        {
            throw new ArgumentException("Storage path is not a safe relative key.", nameof(relativePath));
        }

        var rootFull = Path.GetFullPath(_storage.Root);
        var combined = Path.GetFullPath(Path.Combine(rootFull, relativePath));
        var rootBoundary = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootBoundary, StringComparison.Ordinal))
        {
            throw new ArgumentException("Storage path escapes the storage root.", nameof(relativePath));
        }
        return (rootFull, combined);
    }

    /// <summary>
    /// Read-side resolution helper used by <see cref="Endpoints.StreamController"/>.
    /// Returns null if the file doesn't exist on disk. Same path-traversal
    /// hardening as <see cref="ResolveStrict"/>.
    /// </summary>
    public string? OpenForReadOrNull(string storagePath)
    {
        var (_, full) = ResolveStrict(storagePath);
        return File.Exists(full) ? full : null;
    }

    private void EnsureFreeSpace(string root)
    {
        var info = new DriveInfo(Path.GetPathRoot(root) ?? root);
        if (info.IsReady && info.AvailableFreeSpace < _storage.MinFreeBytes)
        {
            throw new IOException("storage_unavailable: insufficient free disk space.");
        }
    }
}
