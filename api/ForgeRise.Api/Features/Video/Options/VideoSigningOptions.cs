namespace ForgeRise.Api.Features.Video.Options;

/// <summary>
/// Crypto + storage knobs for the Video module's signed-URL contract. Bound
/// from <c>Features:Video</c> alongside <see cref="VideoFeatureOptions"/>.
/// </summary>
public sealed class VideoSigningOptions
{
    public const string SectionName = "Features:Video";

    /// <summary>Server-side HMAC secret. MUST be at least 32 bytes when the module is enabled.</summary>
    public string SigningSecret { get; set; } = string.Empty;

    /// <summary>Default TTL minted for a stream URL when the controller doesn't override.</summary>
    public TimeSpan DefaultUrlTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Hard ceiling on TTL. <see cref="LocalFsObjectStore"/> rejects above this.</summary>
    public TimeSpan MaxUrlTtl { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Strings that must NEVER be allowed as the production signing secret
    /// (security review iter1, finding F4). Lower-cased compare.
    /// </summary>
    public static readonly IReadOnlySet<string> ProductionDenyList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "",
        "change-me",
        "change-me-please",
        "dev-secret",
        "forgerise-dev",
        "secret",
        "password",
    };
}
