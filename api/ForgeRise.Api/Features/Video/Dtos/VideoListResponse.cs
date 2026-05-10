namespace ForgeRise.Api.Features.Video.Dtos;

/// <summary>Empty-by-design listing DTO for V1. Real fields land in V2.</summary>
public sealed record VideoListItemDto(
    Guid Id,
    string OriginalFileName,
    string ProcessingState,
    DateTimeOffset CreatedAt);

public sealed record VideoListResponse(
    IReadOnlyList<VideoListItemDto> Items,
    int Total);
