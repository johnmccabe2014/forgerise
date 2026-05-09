using System.ComponentModel.DataAnnotations;

namespace ForgeRise.Api.Teams.Contracts;

public sealed class CreateTeamRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(40, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "Code must be alphanumeric, underscore, or hyphen.")]
    public string Code { get; init; } = string.Empty;
}

public sealed class UpdateTeamRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;
}

public sealed record TeamDto(Guid Id, string Name, string Code, DateTimeOffset CreatedAt, int PlayerCount);

public sealed class CreatePlayerRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string DisplayName { get; init; } = string.Empty;

    [Range(0, 999)]
    public int? JerseyNumber { get; init; }

    [Range(1900, 2100)]
    public int? BirthYear { get; init; }

    [StringLength(40)]
    public string? Position { get; init; }
}

public sealed class UpdatePlayerRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string DisplayName { get; init; } = string.Empty;

    [Range(0, 999)]
    public int? JerseyNumber { get; init; }

    [Range(1900, 2100)]
    public int? BirthYear { get; init; }

    [StringLength(40)]
    public string? Position { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record PlayerDto(
    Guid Id,
    Guid TeamId,
    string DisplayName,
    int? JerseyNumber,
    int? BirthYear,
    string? Position,
    bool IsActive,
    DateTimeOffset CreatedAt);
