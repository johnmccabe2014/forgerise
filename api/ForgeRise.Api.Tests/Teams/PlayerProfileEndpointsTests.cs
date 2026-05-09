using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Sessions.Contracts;
using ForgeRise.Api.Teams.Contracts;
using ForgeRise.Api.Tests.TestInfra;
using ForgeRise.Api.WelfareModule.Contracts;
using Xunit;

namespace ForgeRise.Api.Tests.Teams;

/// <summary>
/// Per-player profile endpoints used by the web profile page:
/// attendance history (gap-free across team sessions) and the
/// per-player incident summary list.
/// </summary>
public class PlayerProfileEndpointsTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public PlayerProfileEndpointsTests(ForgeRiseFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthenticatedClient(string prefix)
    {
        var client = _factory.CreateDefaultClient(new CookieJarHandler());
        await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"{prefix}-{Guid.NewGuid():n}@example.com",
            password = "Correct horse battery staple",
            displayName = prefix,
        });
        return client;
    }

    private static async Task<TeamDto> CreateTeam(HttpClient c, string code) =>
        (await (await c.PostAsJsonAsync("/teams", new { name = "Squad", code })).Content
            .ReadFromJsonAsync<TeamDto>())!;

    private static async Task<PlayerDto> CreatePlayer(HttpClient c, Guid teamId, string name) =>
        (await (await c.PostAsJsonAsync($"/teams/{teamId}/players", new { displayName = name })).Content
            .ReadFromJsonAsync<PlayerDto>())!;

    private static async Task<SessionDto> CreateSession(HttpClient c, Guid teamId, DateTimeOffset when) =>
        (await (await c.PostAsJsonAsync($"/teams/{teamId}/sessions", new
        {
            scheduledAt = when, durationMinutes = 60, type = SessionType.Training,
        })).Content.ReadFromJsonAsync<SessionDto>())!;

    [Fact]
    public async Task Attendance_history_is_gap_free_and_reverse_chronological()
    {
        var owner = await AuthenticatedClient("pp1");
        var team = await CreateTeam(owner, "pp1-sq");
        var player = await CreatePlayer(owner, team.Id, "Aoife");

        var older = await CreateSession(owner, team.Id, DateTimeOffset.UtcNow.AddDays(-7));
        var middle = await CreateSession(owner, team.Id, DateTimeOffset.UtcNow.AddDays(-3));
        var recent = await CreateSession(owner, team.Id, DateTimeOffset.UtcNow.AddDays(1));

        // Mark only the middle session: Late with a note.
        await owner.PutAsJsonAsync(
            $"/teams/{team.Id}/sessions/{middle.Id}/attendance",
            new { items = new[] { new { playerId = player.Id, status = AttendanceStatus.Late, note = "bus" } } });

        var resp = await owner.GetAsync($"/teams/{team.Id}/players/{player.Id}/attendance");
        resp.EnsureSuccessStatusCode();
        var rows = await resp.Content.ReadFromJsonAsync<List<PlayerAttendanceRowDto>>();

        Assert.Equal(3, rows!.Count);
        // Reverse chronological → most recent (future) first.
        Assert.Equal(recent.Id, rows[0].SessionId);
        Assert.Equal(middle.Id, rows[1].SessionId);
        Assert.Equal(older.Id, rows[2].SessionId);

        Assert.Equal(AttendanceStatus.Absent, rows[0].Status);
        Assert.Equal(AttendanceStatus.Late, rows[1].Status);
        Assert.Equal("bus", rows[1].Note);
        Assert.Equal(AttendanceStatus.Absent, rows[2].Status);
    }

    [Fact]
    public async Task Attendance_for_player_in_other_team_is_404()
    {
        var owner = await AuthenticatedClient("pp2");
        var team = await CreateTeam(owner, "pp2-sq");

        var resp = await owner.GetAsync($"/teams/{team.Id}/players/{Guid.NewGuid()}/attendance");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Attendance_requires_team_membership()
    {
        var owner = await AuthenticatedClient("pp3a");
        var stranger = await AuthenticatedClient("pp3b");
        var team = await CreateTeam(owner, "pp3-sq");
        var player = await CreatePlayer(owner, team.Id, "Niamh");

        var resp = await stranger.GetAsync($"/teams/{team.Id}/players/{player.Id}/attendance");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Per_player_incidents_are_scoped_to_the_player()
    {
        var owner = await AuthenticatedClient("pp4");
        var team = await CreateTeam(owner, "pp4-sq");
        var p1 = await CreatePlayer(owner, team.Id, "P1");
        var p2 = await CreatePlayer(owner, team.Id, "P2");

        await owner.PostAsJsonAsync($"/teams/{team.Id}/players/{p1.Id}/incidents", new
        {
            occurredAt = DateTimeOffset.UtcNow.AddDays(-2),
            severity = IncidentSeverity.Low,
            summary = "Knee knock",
        });
        await owner.PostAsJsonAsync($"/teams/{team.Id}/players/{p2.Id}/incidents", new
        {
            occurredAt = DateTimeOffset.UtcNow.AddDays(-1),
            severity = IncidentSeverity.High,
            summary = "Ankle sprain",
        });

        var p1Resp = await owner.GetAsync($"/teams/{team.Id}/players/{p1.Id}/incidents");
        p1Resp.EnsureSuccessStatusCode();
        var p1Rows = await p1Resp.Content.ReadFromJsonAsync<List<IncidentSummaryDto>>();
        Assert.Single(p1Rows!);
        Assert.Equal("Knee knock", p1Rows![0].Summary);
        Assert.All(p1Rows, r => Assert.Equal(p1.Id, r.PlayerId));

        var p2Resp = await owner.GetAsync($"/teams/{team.Id}/players/{p2.Id}/incidents");
        var p2Rows = await p2Resp.Content.ReadFromJsonAsync<List<IncidentSummaryDto>>();
        Assert.Single(p2Rows!);
        Assert.Equal("Ankle sprain", p2Rows![0].Summary);
    }
}
