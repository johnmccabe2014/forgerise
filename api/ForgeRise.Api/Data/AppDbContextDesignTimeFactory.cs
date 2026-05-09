using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ForgeRise.Api.Data;

/// <summary>
/// Used by `dotnet ef` at design time. Falls back to a placeholder connection
/// string so migrations can be generated without a live database.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=forgerise;Username=forgerise;Password=forgerise")
            .Options;
        return new AppDbContext(options);
    }
}
