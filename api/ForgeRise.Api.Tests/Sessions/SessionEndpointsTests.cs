using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Sessions.Contracts;
using ForgeRise.Api.Tests.TestInfra;
using Xunit;

namespace ForgeRise.Api.Tests.Sessions;

public class SessionEndpointsTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public SessionEndpointsTests(ForgeRiseFactory factory) => _factory = factory;

    private async Task<(HttpClient client, Guid teamId)> Seed(string prefix)
    {
        var client = _factory.CreateDefaultClient(new CookieJarHandler());
        var register = await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"{prefix}-{Guid.NewGuid():n}@example.com",
            password = "Correct horse battery staple",
            displayName = prefix,
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var t = await client.PostAsJsonAsync("/teams", new { name = "Squad", code = $"{prefix}-sq" });
        var team = await t.Content.ReadFromJsonAsync<ForgeRise.Api.Teams.Contracts.TeamDto>();
        return (client, team!.Id);
    }

    [Fact]
    public async Task Create_list_review_and_soft_delete()
    {
        var (client, teamId) = await Seed("hank");

        var create = await client.PostAsJsonAsync($"/teams/{teamId}/sessions", new
        {
            scheduledAt = DateTimeOffset.UtcNow.AddDays(2),
            durationMinutes = 75,
            type = SessionType.Training,
            location = "Pitch 1",
            focus = "Phase play",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var dto = await create.Content.ReadFromJsonAsync<SessionDto>();
        Assert.NotNull(dto);

        var review = await client.PostAsJsonAsync($"/teams/{teamId}/sessions/{dto!.Id}/review",
            new { reviewNotes = "Phases were sharper; defenders cheating offside." });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        var reviewed = await review.Content.ReadFromJsonAsync<SessionDto>();
        Assert.NotNull(reviewed!.ReviewedAt);
        Assert.Contains("Phases", reviewed.ReviewNotes);

        var del = await client.DeleteAsync($"/teams/{teamId}/sessions/{dto.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var list = await client.GetAsync($"/teams/{teamId}/sessions");
        var items = await list.Content.ReadFromJsonAsync<List<SessionDto>>();
        Assert.Empty(items!);
    }

    [Fact]
    public async Task Non_owner_forbidden()
    {
        var (_, teamId) = await Seed("ivy");
        var (stranger, _) = await Seed("jose");

        var resp = await stranger.PostAsJsonAsync($"/teams/{teamId}/sessions", new
        {
            scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            durationMinutes = 60,
            type = SessionType.Training,
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
