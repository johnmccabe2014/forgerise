using ForgeRise.Api.Welfare;
using Xunit;

namespace ForgeRise.Api.Tests.Welfare;

public class ReadinessCategorizerTests
{
    [Fact]
    public void All_nulls_returns_Ready()
    {
        Assert.Equal(SafeCategory.Ready,
            ReadinessCategorizer.Categorize(null, null, null, null, null));
    }

    [Fact]
    public void Light_concern_returns_Monitor()
    {
        // 6.5h sleep (1 point) + soreness 3 (1 point) = 2 points -> Monitor.
        Assert.Equal(SafeCategory.Monitor,
            ReadinessCategorizer.Categorize(6.5, 3, null, null, null));
    }

    [Fact]
    public void Multiple_high_axes_returns_ModifyLoad()
    {
        // soreness 4 (2) + stress 4 (2) + fatigue 3 (1) = 5 -> ModifyLoad.
        Assert.Equal(SafeCategory.ModifyLoad,
            ReadinessCategorizer.Categorize(7.5, 4, null, 4, 3));
    }

    [Fact]
    public void Severe_inputs_return_RecoveryFocus()
    {
        // sleep 3h (3) + soreness 5 (3) + stress 5 (3) -> 9 -> RecoveryFocus.
        Assert.Equal(SafeCategory.RecoveryFocus,
            ReadinessCategorizer.Categorize(3, 5, null, 5, null));
    }

    [Fact]
    public void Bad_mood_alone_can_push_past_Ready()
    {
        // mood 1 (3 points) -> Monitor or ModifyLoad band.
        var category = ReadinessCategorizer.Categorize(8, null, 1, null, null);
        Assert.NotEqual(SafeCategory.Ready, category);
    }

    [Theory]
    [InlineData(8.0, 1, 5, 1, 1, SafeCategory.Ready)]
    [InlineData(7.0, 2, 4, 2, 2, SafeCategory.Ready)]
    [InlineData(7.5, 3, 4, 3, 2, SafeCategory.Monitor)]
    [InlineData(6.5, 3, 3, 3, 3, SafeCategory.ModifyLoad)]
    [InlineData(3.5, 5, 1, 5, 5, SafeCategory.RecoveryFocus)]
    public void Boundary_table(double sleep, int soreness, int mood, int stress, int fatigue, SafeCategory expected)
    {
        Assert.Equal(expected,
            ReadinessCategorizer.Categorize(sleep, soreness, mood, stress, fatigue));
    }
}
