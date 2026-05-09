using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Sessions.Contracts;
using ForgeRise.Api.Tests.TestInfra;
using Xunit;

namespace ForgeRise.Api.Tests.Sessions;

public class AttendanceEndpointsTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public AttendanceEndpointsTests(ForgeRiseFactory factory) => _factory = factory;

    private async Task<(HttpClient client, Guid teamId, Guid sessionId, List<Guid> playerIds)> Seed(string prefix, int playerCount = 3)
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

        var playerIds = new List<Guid>();
        for (var i = 0; i < playerCount; i++)
        {
            var p = await client.PostAsJsonAsync($"/teams/{team!.Id}/players", new { displayName = $"Player {i}" });
            var pdto = await p.Content.ReadFromJsonAsync<ForgeRise.Api.Teams.Contracts.PlayerDto>();
            playerIds.Add(pdto!.Id);
        }

        var s = await client.PostAsJsonAsync($"/teams/{team!.Id}/sessions", new
        {
            scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            durationMinutes = 60,
            type = SessionType.Training,
        });
        var session = await s.Content.ReadFromJsonAsync<SessionDto>();

        return (client, team.Id, session!.Id, playerIds);
    }

    [Fact]
    public async Task Default_list_returns_one_row_per_player_as_absent()
    {
        var (client, teamId, sessionId, playerIds) = await Seed("kai");

        var resp = await client.GetAsync($"/teams/{teamId}/sessions/{sessionId}/attendance");
        resp.EnsureSuccessStatusCode();
        var rows = await resp.Content.ReadFromJsonAsync<List<AttendanceRowDto>>();
        Assert.Equal(playerIds.Count, rows!.Count);
        Assert.All(rows, r => Assert.Equal(AttendanceStatus.Absent, r.Status));
    }

    [Fact]
    public async Task Bulk_upsert_creates_then_updates_in_place()
    {
        var (client, teamId, sessionId, playerIds) = await Seed("lin");

        var first = await client.PutAsJsonAsync(
            $"/teams/{teamId}/sessions/{sessionId}/attendance",
            new
            {
                items = playerIds.Select((id, i) => new
                {
                    playerId = id,
                    status = i == 0 ? AttendanceStatus.Late : AttendanceStatus.Present,
                    note = i == 0 ? "ten min late" : null,
                })
            });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var afterFirst = await first.Content.ReadFromJsonAsync<List<AttendanceRowDto>>();
        Assert.Equal(AttendanceStatus.Late, afterFirst!.Single(r => r.PlayerId == playerIds[0]).Status);
        Assert.Equal("ten min late", afterFirst.Single(r => r.PlayerId == playerIds[0]).Note);

        // Second PUT updates same player to Excused; should not create a duplicate row.
        var second = await client.PutAsJsonAsync(
            $"/teams/{teamId}/sessions/{sessionId}/attendance",
            new { items = new[] { new { playerId = playerIds[0], status = AttendanceStatus.Excused, note = (string?)null } } });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var afterSecond = await second.Content.ReadFromJsonAsync<List<AttendanceRowDto>>();
        Assert.Equal(AttendanceStatus.Excused, afterSecond!.Single(r => r.PlayerId == playerIds[0]).Status);
        Assert.Null(afterSecond.Single(r => r.PlayerId == playerIds[0]).Note);
        // Other players still present from prior PUT.
        Assert.Equal(AttendanceStatus.Present, afterSecond.Single(r => r.PlayerId == playerIds[1]).Status);
    }

    [Fact]
    public async Task Player_outside_team_is_rejected()
    {
        var (client, teamId, sessionId, _) = await Seed("max");

        var resp = await client.PutAsJsonAsync(
            $"/teams/{teamId}/sessions/{sessionId}/attendance",
            new { items = new[] { new { playerId = Guid.NewGuid(), status = AttendanceStatus.Present, note = (string?)null } } });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
