using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Sessions.Contracts;
using ForgeRise.Api.Tests.TestInfra;
using Xunit;

namespace ForgeRise.Api.Tests.Sessions;

public class DrillPreferencesTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public DrillPreferencesTests(ForgeRiseFactory factory) => _factory = factory;

    private async Task<(HttpClient client, Guid teamId)> Seed(string prefix)
    {
        var client = _factory.CreateDefaultClient(new CookieJarHandler());
        await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"{prefix}-{Guid.NewGuid():n}@example.com",
            password = "Correct horse battery staple",
            displayName = prefix,
        });
        var t = await client.PostAsJsonAsync("/teams", new { name = "Squad", code = $"{prefix}-sq" });
        var team = await t.Content.ReadFromJsonAsync<ForgeRise.Api.Teams.Contracts.TeamDto>();
        return (client, team!.Id);
    }

    private sealed record DrillCataloguePrefRow(
        string DrillId, string Title, string Description, int DurationMinutes, List<string> Tags, string? Status,
        DateTimeOffset? UpdatedAt = null, string? LastChangedByDisplayName = null);

    [Fact]
    public async Task List_returns_full_catalogue_with_null_status_by_default()
    {
        var (client, teamId) = await Seed("dp1");
        var rows = await client.GetFromJsonAsync<List<DrillCataloguePrefRow>>(
            $"/teams/{teamId}/drill-preferences");
        Assert.NotNull(rows);
        Assert.True(rows!.Count >= 5);
        Assert.All(rows, r => Assert.Null(r.Status));
    }

    [Fact]
    public async Task Set_records_actor_and_timestamp()
    {
        var (client, teamId) = await Seed("dp-meta");
        (await client.PutAsJsonAsync(
            $"/teams/{teamId}/drill-preferences/conditioned-game", new { status = "favourite" }))
            .EnsureSuccessStatusCode();

        var rows = await client.GetFromJsonAsync<List<DrillCataloguePrefRow>>(
            $"/teams/{teamId}/drill-preferences");
        var row = Assert.Single(rows!, r => r.DrillId == "conditioned-game");
        Assert.Equal("favourite", row.Status);
        Assert.NotNull(row.UpdatedAt);
        Assert.False(string.IsNullOrEmpty(row.LastChangedByDisplayName));
    }

    [Fact]
    public async Task Set_then_clear_round_trips()
    {
        var (client, teamId) = await Seed("dp2");
        var put = await client.PutAsJsonAsync(
            $"/teams/{teamId}/drill-preferences/conditioned-game", new { status = "exclude" });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var rows = await client.GetFromJsonAsync<List<DrillCataloguePrefRow>>(
            $"/teams/{teamId}/drill-preferences");
        var row = Assert.Single(rows!, r => r.DrillId == "conditioned-game");
        Assert.Equal("exclude", row.Status);

        var del = await client.DeleteAsync(
            $"/teams/{teamId}/drill-preferences/conditioned-game");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        rows = await client.GetFromJsonAsync<List<DrillCataloguePrefRow>>(
            $"/teams/{teamId}/drill-preferences");
        Assert.Null(rows!.Single(r => r.DrillId == "conditioned-game").Status);
    }

    [Fact]
    public async Task Excluded_drill_does_not_appear_in_generated_plan_and_favourite_is_prioritised()
    {
        var (client, teamId) = await Seed("dp3");

        // Exclude conditioned-game (a Standard-intensity default) and favourite mobility-flow.
        (await client.PutAsJsonAsync(
            $"/teams/{teamId}/drill-preferences/conditioned-game", new { status = "exclude" }))
            .EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync(
            $"/teams/{teamId}/drill-preferences/mobility-flow", new { status = "favourite" }))
            .EnsureSuccessStatusCode();

        var resp = await client.PostAsJsonAsync($"/teams/{teamId}/session-plans/generate", new { });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<SessionPlanDto>();

        Assert.DoesNotContain(dto!.Recommendations, r => r.DrillId == "conditioned-game");
        var fav = Assert.Single(dto.Recommendations, r => r.DrillId == "mobility-flow");
        Assert.Contains("team favourite", fav.Rationale);
    }

    [Fact]
    public async Task Set_with_invalid_status_returns_400()
    {
        var (client, teamId) = await Seed("dp4");
        var resp = await client.PutAsJsonAsync(
            $"/teams/{teamId}/drill-preferences/mobility-flow", new { status = "love-it" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Set_for_unknown_drill_returns_404()
    {
        var (client, teamId) = await Seed("dp5");
        var resp = await client.PutAsJsonAsync(
            $"/teams/{teamId}/drill-preferences/no-such-drill", new { status = "favourite" });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Endpoints_are_team_scoped()
    {
        var (owner, ownedTeam) = await Seed("dp6a");
        var (stranger, _) = await Seed("dp6b");

        var get = await stranger.GetAsync($"/teams/{ownedTeam}/drill-preferences");
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        var put = await stranger.PutAsJsonAsync(
            $"/teams/{ownedTeam}/drill-preferences/mobility-flow", new { status = "favourite" });
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);

        // Owner can still write.
        (await owner.PutAsJsonAsync(
            $"/teams/{ownedTeam}/drill-preferences/mobility-flow", new { status = "favourite" }))
            .EnsureSuccessStatusCode();
    }
}
