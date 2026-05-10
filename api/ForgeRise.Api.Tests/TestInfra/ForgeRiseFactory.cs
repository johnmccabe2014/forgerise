using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeRise.Api.Tests.TestInfra;

/// <summary>
/// Custom factory: switches Environment to "Testing", swaps the DbContext to
/// EF InMemory, and forces a deterministic JWT key. Each instance gets its
/// own InMemory database name.
/// </summary>
public sealed class ForgeRiseFactory : WebApplicationFactory<Program>
{
    public string DbName { get; } = $"forgerise-tests-{Guid.NewGuid():n}";
    public const string JwtKey = "test-jwt-key-must-be-at-least-32-chars-long-12345";

    /// <summary>
    /// Extra in-memory configuration applied at host build. Set by
    /// <see cref="ForgeRiseFactoryExtensions"/> helpers BEFORE any client
    /// is created.
    /// </summary>
    public Dictionary<string, string?> ExtraConfig { get; } = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureHostConfiguration(c =>
        {
            var baseSettings = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = JwtKey,
                ["Jwt:Issuer"] = "forgerise.tests",
                ["Jwt:Audience"] = "forgerise.tests",
                ["ConnectionStrings:Postgres"] = string.Empty,
                ["Cors:AllowedOrigins"] = string.Empty,
            };
            c.AddInMemoryCollection(baseSettings);
            if (ExtraConfig.Count > 0) c.AddInMemoryCollection(ExtraConfig);
        });
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace any prior AppDbContext registration with InMemory.
            var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(DbName));

            // Replace the JwtOptions with one keyed to JwtKey for tests.
            var prior = services.FirstOrDefault(d => d.ServiceType == typeof(JwtOptions));
            if (prior is not null) services.Remove(prior);
            services.AddSingleton(new JwtOptions
            {
                Key = JwtKey,
                Issuer = "forgerise.tests",
                Audience = "forgerise.tests",
            });
        });
    }
}
