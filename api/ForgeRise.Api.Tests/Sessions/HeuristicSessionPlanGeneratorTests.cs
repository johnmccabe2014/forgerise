using ForgeRise.Api.Sessions;
using ForgeRise.Api.Welfare;
using Xunit;

namespace ForgeRise.Api.Tests.Sessions;

public class HeuristicSessionPlanGeneratorTests
{
    private static SessionPlanContext Ctx(IReadOnlyList<PlayerReadiness> readiness, string? focus = null, string? prevFocus = null) =>
        new(Guid.NewGuid(), focus, prevFocus, null, DateTimeOffset.UtcNow, readiness);

    [Fact]
    public async Task All_ready_yields_standard_intensity_and_default_focus()
    {
        var gen = new HeuristicSessionPlanGenerator();
        var ids = Enumerable.Range(0, 5).Select(_ => new PlayerReadiness(Guid.NewGuid(), SafeCategory.Ready)).ToList();
        var plan = await gen.GenerateAsync(Ctx(ids));
        Assert.Equal("General conditioning + skills", plan.Focus);
        Assert.All(plan.Blocks, b => Assert.Equal("Standard", b.Intensity));
        Assert.Equal(5, plan.Blocks.Count);
    }

    [Fact]
    public async Task ModifyLoad_majority_yields_reduced()
    {
        var gen = new HeuristicSessionPlanGenerator();
        var roster = new[]
        {
            new PlayerReadiness(Guid.NewGuid(), SafeCategory.ModifyLoad),
            new PlayerReadiness(Guid.NewGuid(), SafeCategory.ModifyLoad),
            new PlayerReadiness(Guid.NewGuid(), SafeCategory.Ready),
        };
        var plan = await gen.GenerateAsync(Ctx(roster));
        Assert.All(plan.Blocks, b => Assert.Equal("Reduced", b.Intensity));
    }

    [Fact]
    public async Task Recovery_focus_threshold_yields_recovery_emphasis()
    {
        var gen = new HeuristicSessionPlanGenerator();
        var roster = new[]
        {
            new PlayerReadiness(Guid.NewGuid(), SafeCategory.RecoveryFocus),
            new PlayerReadiness(Guid.NewGuid(), SafeCategory.Ready),
            new PlayerReadiness(Guid.NewGuid(), SafeCategory.Ready),
            new PlayerReadiness(Guid.NewGuid(), SafeCategory.Ready),
            new PlayerReadiness(Guid.NewGuid(), SafeCategory.Ready),
        };
        var plan = await gen.GenerateAsync(Ctx(roster));
        Assert.All(plan.Blocks, b => Assert.Equal("Recovery emphasis", b.Intensity));
    }

    [Fact]
    public async Task Explicit_focus_override_is_honoured()
    {
        var gen = new HeuristicSessionPlanGenerator();
        var plan = await gen.GenerateAsync(Ctx(Array.Empty<PlayerReadiness>(), focus: "Lineout pods"));
        Assert.Equal("Lineout pods", plan.Focus);
    }

    [Fact]
    public async Task Empty_roster_still_produces_five_block_plan()
    {
        var gen = new HeuristicSessionPlanGenerator();
        var plan = await gen.GenerateAsync(Ctx(Array.Empty<PlayerReadiness>()));
        Assert.Equal(5, plan.Blocks.Count);
        Assert.Contains("No readiness data", plan.Summary);
    }

    [Fact]
    public async Task Previous_focus_is_carried_when_no_override()
    {
        var gen = new HeuristicSessionPlanGenerator();
        var plan = await gen.GenerateAsync(Ctx(Array.Empty<PlayerReadiness>(), prevFocus: "Defensive line speed"));
        Assert.Equal("Defensive line speed", plan.Focus);
    }
}
