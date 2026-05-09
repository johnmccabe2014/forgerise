using ForgeRise.Api.Sessions;
using Xunit;

namespace ForgeRise.Api.Tests.Sessions;

public class DrillRecommenderTests
{
    [Fact]
    public void Recovery_intensity_only_returns_low_contact_drills()
    {
        var picks = DrillRecommender.Recommend(
            intensity: "Recovery emphasis",
            focus: null,
            hasRecentSelfIncident: false);

        Assert.NotEmpty(picks);
        Assert.All(picks, p => Assert.Contains("low_contact", p.Tags));
    }

    [Fact]
    public void Recent_self_incident_forces_low_contact_even_for_standard_intensity()
    {
        var picks = DrillRecommender.Recommend(
            intensity: "Standard",
            focus: "attack shape",
            hasRecentSelfIncident: true);

        Assert.NotEmpty(picks);
        Assert.All(picks, p => Assert.Contains("low_contact", p.Tags));
        // Rationale should mention the incident bias on at least one pick.
        Assert.Contains(picks, p => p.Rationale.Contains("self-reported incident", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Focus_keyword_biases_first_picks()
    {
        var picks = DrillRecommender.Recommend(
            intensity: "Standard",
            focus: "scrum sequencing",
            hasRecentSelfIncident: false);

        Assert.Contains(picks, p => p.DrillId == "scrum-engage");
        // Rationale of the scrum pick mentions focus match.
        var scrum = picks.First(p => p.DrillId == "scrum-engage");
        Assert.Contains("focus", scrum.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Always_returns_at_least_two_recommendations()
    {
        var picks = DrillRecommender.Recommend(
            intensity: "Recovery emphasis",
            focus: null,
            hasRecentSelfIncident: true);

        Assert.True(picks.Count >= 2);
    }
}
