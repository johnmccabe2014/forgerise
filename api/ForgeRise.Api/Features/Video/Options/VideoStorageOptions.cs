namespace ForgeRise.Api.Features.Video.Options;

/// <summary>
/// Filesystem + quota knobs for V2's <c>LocalFsObjectStore</c>. Bound from
/// <c>Features:Video</c>.
/// </summary>
public sealed class VideoStorageOptions
{
    public const string SectionName = "Features:Video";

    /// <summary>
    /// Root directory under which all team prefixes live. MUST exist and be
    /// writable when the module is enabled. In tests, the factory sets this
    /// to a per-run temp dir.
    /// </summary>
    public string Root { get; set; } = string.Empty;

    /// <summary>Per-upload hard cap. Default 500 MiB.</summary>
    public long MaxUploadBytes { get; set; } = 500L * 1024 * 1024;

    /// <summary>Per-team total quota across all non-deleted assets. Default 5 GiB.</summary>
    public long TeamQuotaBytes { get; set; } = 5L * 1024 * 1024 * 1024;

    /// <summary>
    /// If the disk has less than this much free space, refuse uploads with a
    /// deterministic 503 (security review iter1, finding F3). Default 1 GiB.
    /// </summary>
    public long MinFreeBytes { get; set; } = 1L * 1024 * 1024 * 1024;
}
