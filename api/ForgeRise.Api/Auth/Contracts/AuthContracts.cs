using System.ComponentModel.DataAnnotations;

namespace ForgeRise.Api.Auth.Contracts;

public sealed record RegisterRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 1)]
    public string DisplayName { get; init; } = string.Empty;
}

public sealed record LoginRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 1)]
    public string Password { get; init; } = string.Empty;
}

public sealed record AuthUserDto(Guid Id, string Email, string DisplayName);

public sealed record AuthResponse(AuthUserDto User);
