using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities.Video;
using ForgeRise.Api.Tests.TestInfra;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ForgeRise.Api.Tests.Video;

/// <summary>
/// V2 upload tests. Cover the security-critical contract: MIME sniff,
/// quota, byte cap, flag gate, non-member, cross-team isolation.
/// </summary>
public class VideoUploadTests
{
    private static byte[] ValidMp4Header() => new byte[]
    {
        0, 0, 0, 32,                  // box size (any)
        (byte)'f', (byte)'t', (byte)'y', (byte)'p',
        (byte)'i', (byte)'s', (byte)'o', (byte)'m', // brand
        0, 0, 0, 1,
        // pad
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    };

    private static MultipartFormDataContent FilePart(byte[] body, string name = "training.mp4")
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        var form = new MultipartFormDataContent();
        form.Add(content, "file", name);
        return form;
    }

    private static async Task<(HttpClient client, Guid teamId)> SeedTeam(
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
        return (client, team!.Id);
    }

    [Fact]
    public async Task Upload_returns_404_when_feature_disabled()
    {
        await using var factory = new ForgeRiseFactory();
        var (client, teamId) = await SeedTeam(factory, "up-off");
        var resp = await client.PostAsync($"/v1/teams/{teamId}/videos/uploads",
            FilePart(ValidMp4Header()));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Upload_201_for_member_with_valid_mp4_header()
    {
        await using var factory = new ForgeRiseFactory().WithVideoEnabled();
        var (client, teamId) = await SeedTeam(factory, "up-ok");

        var resp = await client.PostAsync($"/v1/teams/{teamId}/videos/uploads",
            FilePart(ValidMp4Header()));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        // Asset row exists, byte file exists, processing state is Queued.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asset = db.VideoAssets.Single(a => a.TeamId == teamId);
        Assert.Equal(VideoProcessingState.Queued, asset.ProcessingState);
        Assert.False(string.IsNullOrEmpty(asset.ContentSha256));
        Assert.Equal(64, asset.ContentSha256!.Length);
        Assert.True(asset.SizeBytes > 0);

        var root = factory.ExtraConfig["Features:Video:Root"]!;
        Assert.True(File.Exists(Path.Combine(root, asset.StoragePath)));
    }

    [Fact]
    public async Task Upload_returns_415_for_non_mp4_payload()
    {
        await using var factory = new ForgeRiseFactory().WithVideoEnabled();
        var (client, teamId) = await SeedTeam(factory, "up-bad");

        // PNG signature, definitely not an ftyp box.
        var bogus = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0 };
        var resp = await client.PostAsync($"/v1/teams/{teamId}/videos/uploads", FilePart(bogus, "fake.mp4"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, resp.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, db.VideoAssets.Count(a => a.TeamId == teamId));
    }

    [Fact]
    public async Task Upload_returns_403_for_non_member()
    {
        await using var factory = new ForgeRiseFactory().WithVideoEnabled();
        var (_, teamId) = await SeedTeam(factory, "up-owner");
        var (stranger, _) = await SeedTeam(factory, "up-stranger");

        var resp = await stranger.PostAsync($"/v1/teams/{teamId}/videos/uploads",
            FilePart(ValidMp4Header()));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Upload_returns_413_when_team_quota_already_full()
    {
        await using var factory = new ForgeRiseFactory().WithVideoEnabled();
        // Tight quota so a single fake row fills it.
        factory.ExtraConfig["Features:Video:TeamQuotaBytes"] = "100";
        var (client, teamId) = await SeedTeam(factory, "up-quota");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.VideoAssets.Add(new VideoAsset
            {
                TeamId = teamId,
                CreatedByUserId = Guid.NewGuid(),
                OriginalFileName = "old.mp4",
                MimeType = "video/mp4",
                SizeBytes = 200,
                StoragePath = $"teams/{teamId:N}/raw/old.mp4",
            });
            await db.SaveChangesAsync();
        }

        var resp = await client.PostAsync($"/v1/teams/{teamId}/videos/uploads",
            FilePart(ValidMp4Header()));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resp.StatusCode);
    }

    [Fact]
    public async Task Upload_status_endpoint_returns_state()
    {
        await using var factory = new ForgeRiseFactory().WithVideoEnabled();
        var (client, teamId) = await SeedTeam(factory, "status");

        var up = await client.PostAsync($"/v1/teams/{teamId}/videos/uploads",
            FilePart(ValidMp4Header()));
        Assert.Equal(HttpStatusCode.Created, up.StatusCode);
        var body = await up.Content.ReadFromJsonAsync<UploadResponseShape>();

        var st = await client.GetAsync($"/v1/teams/{teamId}/videos/{body!.VideoAssetId}/status");
        Assert.Equal(HttpStatusCode.OK, st.StatusCode);
        var s = await st.Content.ReadFromJsonAsync<StatusShape>();
        Assert.Equal("Queued", s!.ProcessingState);
    }

    public sealed record UploadResponseShape(
        Guid VideoAssetId,
        string OriginalFileName,
        long SizeBytes,
        string ContentSha256,
        string ProcessingState,
        DateTimeOffset CreatedAt);

    public sealed record StatusShape(Guid VideoAssetId, string ProcessingState, string? ProcessingError);
}
