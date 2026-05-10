using ForgeRise.Api.Data.Entities.Video;

namespace ForgeRise.Api.Features.Video.Services;

/// <summary>
/// V2 single-shot video upload. The controller calls
/// <see cref="UploadAsync"/> after auth + team scope. The service owns:
/// MIME sniff (12-byte ftyp prefix), per-team quota, streaming write,
/// SHA-256 hashing, and persistence of <see cref="VideoUploadSession"/> +
/// <see cref="VideoAsset"/> rows.
/// </summary>
public interface IUploadService
{
    Task<UploadOutcome> UploadAsync(
        Guid teamId,
        Guid uploaderUserId,
        string originalFileName,
        Stream body,
        CancellationToken ct);
}

/// <summary>Tagged-union result. Exactly one of the fields is non-null.</summary>
public sealed record UploadOutcome(
    VideoAsset? Asset,
    UploadFailure? Failure);

public enum UploadFailure
{
    UnsupportedMediaType,
    PayloadTooLarge,
    TeamQuotaExceeded,
    StorageUnavailable,
}
