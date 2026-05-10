using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities.Video;
using ForgeRise.Api.Tests.TestInfra;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ForgeRise.Api.Tests.Video;

/// <summary>
/// V1 endpoint tests: feature-flag default off, flag-on returns empty page,
/// non-member is forbidden, cross-team isolation holds for both
/// <see cref="VideoAsset"/> (top-level) and <see cref="VideoTimelineEvent"/>
/// (child) — matches security review iter1 finding #3.
/// </summary>
public class VideoEndpointsTests
{
    private static async Task<(HttpClient client, Guid teamId, Guid userId)> SeedTeam(
        ForgeRiseFactory factory, string prefix)
    {
        var client = factory.CreateDefaultClient(new CookieJarHandler());
        await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"{prefix}-{Guid.NewGuid():n}@example.com",
            password = "Correct horse battery staple",
            displayName = prefix,
        });
        var t = await client.PostAsJsonAsync("/teams", new { name = "Squad", code = $"{prefix}-sq" });
        var team = await t.Content.ReadFromJsonAsync<ForgeRise.Api.Teams.Contracts.TeamDto>();

        // Lookup the owner user id via DbContext — TeamDto doesn't expose it.
        Guid ownerId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ownerId = db.Teams.First(x => x.Id == team!.Id).OwnerUserId;
        }
        return (client, team!.Id, ownerId);
    }

    [Fact]
    public async Task List_returns_404_when_feature_disabled_by_default()
    {
        await using var factory = new ForgeRiseFactory();
        var (client, teamId, _) = await SeedTeam(factory, "vid-off");

        var resp = await client.GetAsync($"/v1/teams/{teamId}/videos");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task List_returns_empty_page_when_feature_enabled_for_team_member()
    {
        await using var factory = new ForgeRiseFactory().WithVideoEnabled();
        var (client, teamId, _) = await SeedTeam(factory, "vid-on");

        var resp = await client.GetAsync($"/v1/teams/{teamId}/videos");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<VideoListResponseShape>();
        Assert.NotNull(body);
        Assert.Empty(body!.Items);
        Assert.Equal(0, body.Total);
    }

    [Fact]
    public async Task List_returns_403_for_non_member_when_feature_enabled()
    {
        await using var factory = new ForgeRiseFactory().WithVideoEnabled();
        var (_, teamId, _) = await SeedTeam(factory, "vid-owner");
        var (otherClient, _, _) = await SeedTeam(factory, "vid-stranger");

        var resp = await otherClient.GetAsync($"/v1/teams/{teamId}/videos");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task List_for_team_B_does_not_see_team_A_VideoAsset()
    {
        await using var factory = new ForgeRiseFactory().WithVideoEnabled();
        var (clientA, teamAId, userA) = await SeedTeam(factory, "iso-a");
        var (clientB, teamBId, _) = await SeedTeam(factory, "iso-b");

        // Seed a VideoAsset owned by team A directly via the DbContext.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.VideoAssets.Add(new VideoAsset
            {
                TeamId = teamAId,
                CreatedByUserId = userA,
                OriginalFileName = "match.mp4",
                MimeType = "video/mp4",
                SizeBytes = 1024,
                StoragePath = $"teams/{teamAId}/source.mp4",
            });
            await db.SaveChangesAsync();
        }

        // Team B's listing must be empty.
        var bResp = await clientB.GetAsync($"/v1/teams/{teamBId}/videos");
        Assert.Equal(HttpStatusCode.OK, bResp.StatusCode);
        var b = await bResp.Content.ReadFromJsonAsync<VideoListResponseShape>();
        Assert.Empty(b!.Items);

        // V2: team A now actually sees its own asset (no Take(0)).
        var aResp = await clientA.GetAsync($"/v1/teams/{teamAId}/videos");
        var a = await aResp.Content.ReadFromJsonAsync<VideoListResponseShape>();
        Assert.Single(a!.Items);
        Assert.Equal("match.mp4", a.Items[0].OriginalFileName);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, db.VideoAssets.Count(a => a.TeamId == teamAId));
            Assert.Equal(0, db.VideoAssets.Count(a => a.TeamId == teamBId));
        }
    }

    [Fact]
    public async Task VideoTimelineEvent_is_isolated_per_team_at_storage_layer()
    {
        // Per security review iter1 finding #3 — second isolation test on a
        // child entity, so the regression guard catches both top-level and
        // nested table leaks.
        await using var factory = new ForgeRiseFactory().WithVideoEnabled();
        var (_, teamAId, userA) = await SeedTeam(factory, "iso-evt-a");
        var (_, teamBId, _) = await SeedTeam(factory, "iso-evt-b");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var asset = new VideoAsset
        {
            TeamId = teamAId,
            CreatedByUserId = userA,
            OriginalFileName = "match.mp4",
            MimeType = "video/mp4",
            SizeBytes = 2048,
            StoragePath = $"teams/{teamAId}/source.mp4",
        };
        db.VideoAssets.Add(asset);
        db.VideoTimelineEvents.Add(new VideoTimelineEvent
        {
            TeamId = teamAId,
            VideoAsset = asset,
            VideoAssetId = asset.Id,
            AtSeconds = 12.5,
            Kind = VideoTimelineEventKind.Note,
            Body = "great line break",
            CreatedByUserId = userA,
        });
        await db.SaveChangesAsync();

        Assert.Equal(1, db.VideoTimelineEvents.Count(x => x.TeamId == teamAId));
        Assert.Equal(0, db.VideoTimelineEvents.Count(x => x.TeamId == teamBId));
    }

    private sealed record VideoListResponseShape(List<VideoListItemShape> Items, int Total);
    private sealed record VideoListItemShape(Guid Id, string OriginalFileName, string ProcessingState, DateTimeOffset CreatedAt);
}
