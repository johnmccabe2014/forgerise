namespace ForgeRise.Api.Features.Video.Dtos;

/// <summary>
/// Returned from <c>POST /v1/teams/{teamId}/videos/uploads</c>. The
/// <c>processingState</c> will be <c>"Queued"</c> in V2 because no worker
/// runs yet (V3 introduces ffprobe + state transitions).
/// </summary>
public sealed record UploadResponse(
    Guid VideoAssetId,
    string OriginalFileName,
    long SizeBytes,
    string ContentSha256,
    string ProcessingState,
    DateTimeOffset CreatedAt);

/// <summary>Cheap polling endpoint payload.</summary>
public sealed record VideoStatusResponse(
    Guid VideoAssetId,
    string ProcessingState,
    string? ProcessingError);
