using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ForgeRise.Api.Data;
using ForgeRise.Api.Features.Video.Storage;
using ForgeRise.Api.Tests.TestInfra;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ForgeRise.Api.Tests.Video;

/// <summary>
/// V2 stream tests: signed-URL HMAC contract end to end (mint → 302 → 200),
/// tampered sig → 401, expired exp → 410, no-referrer + no-store headers,
/// path traversal hardening on <see cref="LocalFsObjectStore"/>.
/// </summary>
public class VideoStreamTests
{
    private static byte[] ValidMp4Header() => new byte[]
    {
        0, 0, 0, 32,
        (byte)'f', (byte)'t', (byte)'y', (byte)'p',
        (byte)'i', (byte)'s', (byte)'o', (byte)'m',
        0, 0, 0, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    };

    private static MultipartFormDataContent FilePart(byte[] body)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        var form = new MultipartFormDataContent();
        form.Add(content, "file", "training.mp4");
        return form;
    }

    private static async Task<(HttpClient client, Guid teamId, Guid assetId)> SeedAndUpload(
        ForgeRiseFactory factory)
    {
        var client = factory.CreateDefaultClient(new CookieJarHandler());
        await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"sm-{Guid.NewGuid():n}@example.com",
            password = "Correct horse battery staple",
            displayName = "stream",
        });
        var t = await client.PostAsJsonAsync("/teams", new { name = "Squad", code = $"st-{Guid.NewGuid():n}" });
        var team = await t.Content.ReadFromJsonAsync<ForgeRise.Api.Teams.Contracts.TeamDto>();

        var up = await client.PostAsync($"/v1/teams/{team!.Id}/videos/uploads", FilePart(ValidMp4Header()));
        Assert.Equal(HttpStatusCode.Created, up.StatusCode);
        var body = await up.Content.ReadFromJsonAsync<VideoUploadTests.UploadResponseShape>();
        return (client, team.Id, body!.VideoAssetId);
    }

    [Fact]
    public async Task Mint_to_blob_returns_200_with_no_referrer_and_no_store()
    {
        await using var factory = new ForgeRiseFactory().WithVideoEnabled();
        var (client, teamId, assetId) = await SeedAndUpload(factory);

        // Step 1: mint the signed URL. The TestHost client does not
        // auto-follow redirects, so we inspect the 302 and follow manually.
        var mint = await client.GetAsync($"/v1/teams/{teamId}/videos/{assetId}/stream");
        Assert.Equal(HttpStatusCode.Redirect, mint.StatusCode);
        Assert.Equal("no-referrer", mint.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("no-store", mint.Headers.CacheControl?.ToString() ?? string.Empty);
        var location = mint.Headers.Location;
        Assert.NotNull(location);

        // Step 2: hit the signed blob URL. Hardening headers must also be
        // present on the final 200 (so caches on the path don't store the
        // bytes either).
        var blob = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, blob.StatusCode);
        Assert.Equal("no-referrer", blob.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("no-store", blob.Headers.CacheControl?.ToString() ?? string.Empty);

        var bytes = await blob.Content.ReadAsByteArrayAsync();
        Assert.Equal(ValidMp4Header().Length, bytes.Length);
    }

    [Fact]
    public async Task Blob_returns_401_when_signature_is_tampered()
    {
        await using var factory = new ForgeRiseFactory().WithVideoEnabled();
        var (client, _, assetId) = await SeedAndUpload(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var path = db.VideoAssets.Single(a => a.Id == assetId).StoragePath;
        var secret = Encoding.UTF8.GetBytes(factory.ExtraConfig["Features:Video:SigningSecret"]!);

        var exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var viewer = Guid.NewGuid();
        var goodSig = SignedUrlForTests.Sign(path, viewer, exp, secret);
        var tampered = goodSig[..^2] + "00";

        var bad = await client.GetAsync(
            $"/v1/videos/blob?path={Uri.EscapeDataString(path)}&v={viewer:N}&exp={exp}&sig={tampered}");
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
    }

    [Fact]
    public async Task Blob_returns_410_when_signature_is_expired()
    {
        await using var factory = new ForgeRiseFactory().WithVideoEnabled();
        var (client, _, assetId) = await SeedAndUpload(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asset = db.VideoAssets.Single(a => a.Id == assetId);
        var secret = Encoding.UTF8.GetBytes(factory.ExtraConfig["Features:Video:SigningSecret"]!);

        var exp = DateTimeOffset.UtcNow.AddSeconds(-30).ToUnixTimeSeconds();
        var viewer = Guid.NewGuid();
        var sig = SignedUrlForTests.Sign(asset.StoragePath, viewer, exp, secret);

        var resp = await client.GetAsync(
            $"/v1/videos/blob?path={Uri.EscapeDataString(asset.StoragePath)}&v={viewer:N}&exp={exp}&sig={sig}");
        Assert.Equal(HttpStatusCode.Gone, resp.StatusCode);
    }

    [Fact]
    public void LocalFsObjectStore_rejects_path_traversal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"forgerise-trav-{Guid.NewGuid():n}");
        Directory.CreateDirectory(root);
        var storage = Microsoft.Extensions.Options.Options.Create(
            new ForgeRise.Api.Features.Video.Options.VideoStorageOptions { Root = root });
        var signing = Microsoft.Extensions.Options.Options.Create(
            new ForgeRise.Api.Features.Video.Options.VideoSigningOptions
            {
                SigningSecret = "test-signing-secret-must-be-at-least-32-bytes-of-entropy-here-12",
            });
        var store = new LocalFsObjectStore(storage, signing, TimeProvider.System);

        Assert.Throws<ArgumentException>(() => store.ResolveStrict("../../../etc/passwd"));
        Assert.Throws<ArgumentException>(() => store.ResolveStrict("teams/x/../../etc/passwd"));
        Assert.Throws<ArgumentException>(() => store.ResolveStrict("/absolute"));
        Assert.Throws<ArgumentException>(() => store.ResolveStrict("\0"));
        Assert.Throws<ArgumentException>(() => store.ResolveStrict(string.Empty));
        Assert.Throws<ArgumentException>(() => store.ResolveStrict("teams\\bad"));
        Assert.Throws<ArgumentException>(() => store.ResolveStrict("teams/c:bad"));
    }

    [Fact]
    public async Task SignedUrl_TTL_above_max_throws()
    {
        var root = Path.Combine(Path.GetTempPath(), $"forgerise-ttl-{Guid.NewGuid():n}");
        Directory.CreateDirectory(root);
        var storage = Microsoft.Extensions.Options.Options.Create(
            new ForgeRise.Api.Features.Video.Options.VideoStorageOptions { Root = root });
        var signing = Microsoft.Extensions.Options.Options.Create(
            new ForgeRise.Api.Features.Video.Options.VideoSigningOptions
            {
                SigningSecret = "test-signing-secret-must-be-at-least-32-bytes-of-entropy-here-12",
                MaxUrlTtl = TimeSpan.FromMinutes(15),
            });
        var store = new LocalFsObjectStore(storage, signing, TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await store.GetSignedReadUrlAsync(
                "teams/x/raw/file.mp4",
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(60),
                CancellationToken.None));
    }
}

/// <summary>Mirror of the production SignedUrl helper for test-side computation.</summary>
internal static class SignedUrlForTests
{
    public static string Sign(string path, Guid viewer, long exp, byte[] secret)
    {
        var msg = $"{path}|{viewer:N}|{exp}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(secret);
        var mac = hmac.ComputeHash(Encoding.UTF8.GetBytes(msg));
        return Convert.ToHexString(mac).ToLowerInvariant();
    }
}
