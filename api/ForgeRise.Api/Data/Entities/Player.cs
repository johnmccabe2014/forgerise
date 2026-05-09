namespace ForgeRise.Api.Data.Entities;

public sealed class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public required string DisplayName { get; set; }
    public int? JerseyNumber { get; set; }
    public int? BirthYear { get; set; }
    public string? Position { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}
