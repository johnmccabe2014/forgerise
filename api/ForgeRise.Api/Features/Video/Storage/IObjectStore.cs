namespace ForgeRise.Api.Features.Video.Storage;

/// <summary>
/// Abstraction over the bytes store backing video assets. V1 ships the
/// interface only; V2 adds a <c>LocalFsObjectStore</c> implementation,
/// V3 adds an S3/MinIO implementation. Callers MUST treat
/// <see cref="StoragePath"/> values as opaque.
/// </summary>
public interface IObjectStore
{
    /// <summary>
    /// Persist a stream and return the opaque storage path that subsequent
    /// reads/deletes will use.
    /// </summary>
    Task<string> PutAsync(
        string suggestedRelativePath,
        Stream content,
        string contentType,
        CancellationToken ct);

    /// <summary>
    /// Mint a short-lived signed URL that allows a single viewer to read
    /// the object. Implementations MUST HMAC
    /// <paramref name="storagePath"/>, <paramref name="expiresAt"/> and
    /// <paramref name="viewerUserId"/> together so a URL cannot be replayed
    /// against a different viewer (security review iter1, finding #4).
    /// </summary>
    /// <param name="storagePath">Opaque path returned by <see cref="PutAsync"/>.</param>
    /// <param name="viewerUserId">The user id the URL is being minted for.</param>
    /// <param name="expiresAt">Hard expiry. Implementations should reject TTLs &gt; 15 minutes.</param>
    Task<Uri> GetSignedReadUrlAsync(
        string storagePath,
        Guid viewerUserId,
        DateTimeOffset expiresAt,
        CancellationToken ct);

    /// <summary>Best-effort delete. Idempotent: missing path is not an error.</summary>
    Task DeleteAsync(string storagePath, CancellationToken ct);
}
