using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Sessions;
using ForgeRise.Api.Tests.TestInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ForgeRise.Api.Tests.Sessions;

public class DemoSeederTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public DemoSeederTests(ForgeRiseFactory factory) => _factory = factory;

    private DemoSeeder Resolve(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var gen = scope.ServiceProvider.GetRequiredService<ISessionPlanGenerator>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        return new DemoSeeder(db, hasher, gen, time, NullLogger<DemoSeeder>.Instance);
    }

    [Fact]
    public async Task First_run_seeds_coach_two_teams_and_plans()
    {
        // Use a fresh factory so this test has an isolated InMemory DB —
        // without it, the seeder would see the demo coach from a prior test.
        await using var factory = new ForgeRiseFactory();
        _ = factory.CreateDefaultClient(); // forces host build
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = Resolve(scope);

        var inserted = await seeder.SeedAsync();

        Assert.True(inserted);
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == DemoSeeder.CoachEmail));
        Assert.Equal(2, await db.Teams.CountAsync());
        Assert.True(await db.Players.CountAsync() >= 30);
        // Every player must have a wellness check-in so the readiness chart
        // is populated on first login.
        Assert.Equal(await db.Players.CountAsync(), await db.WellnessCheckIns.CountAsync());
        // One past reviewed session per team + one generated plan per team.
        Assert.Equal(2, await db.Sessions.CountAsync(s => s.ReviewedAt != null));
        Assert.Equal(2, await db.SessionPlans.CountAsync());
    }

    [Fact]
    public async Task Re_running_seed_is_a_no_op()
    {
        await using var factory = new ForgeRiseFactory();
        _ = factory.CreateDefaultClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = Resolve(scope);

        Assert.True(await seeder.SeedAsync());
        var teamCount = await db.Teams.CountAsync();
        var playerCount = await db.Players.CountAsync();
        var planCount = await db.SessionPlans.CountAsync();

        // Resolve a fresh seeder from a new scope (so the AppDbContext is
        // fresh too) and confirm the second pass touches nothing.
        using var scope2 = factory.Services.CreateScope();
        var seeder2 = Resolve(scope2);
        Assert.False(await seeder2.SeedAsync());

        Assert.Equal(teamCount, await db.Teams.CountAsync());
        Assert.Equal(playerCount, await db.Players.CountAsync());
        Assert.Equal(planCount, await db.SessionPlans.CountAsync());
    }
}
