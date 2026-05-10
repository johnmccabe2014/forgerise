namespace ForgeRise.Api.Features.Video.Storage;

/// <summary>
/// Necessary-not-sufficient byte-level check that an upload is an ISO-BMFF
/// MP4. The first 12 bytes of every valid mp4 begin with a <c>ftyp</c> box:
/// <c>[size:4][b'ftyp'][major brand:4]</c>. We accept a small allow-list of
/// brands that cover phone-recorded H.264/H.265 footage. ffprobe in V3 is the
/// authoritative gate before <see cref="VideoProcessingState.Ready"/>.
/// </summary>
internal static class VideoMimeSniffer
{
    private const string MimeMp4 = "video/mp4";

    private static readonly string[] AcceptedBrands =
    {
        "isom", "iso2", "mp41", "mp42", "avc1", "M4V ", "M4A ", "qt  ",
    };

    /// <summary>
    /// Returns the canonical MIME type when the prefix matches an accepted
    /// ftyp brand. Returns null otherwise — callers MUST treat null as a
    /// hard reject (415).
    /// </summary>
    public static string? Sniff(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length < 12) return null;
        if (prefix[4] != (byte)'f' || prefix[5] != (byte)'t' ||
            prefix[6] != (byte)'y' || prefix[7] != (byte)'p')
        {
            return null;
        }
        var brand = System.Text.Encoding.ASCII.GetString(prefix.Slice(8, 4));
        foreach (var ok in AcceptedBrands)
        {
            if (string.Equals(brand, ok, StringComparison.Ordinal)) return MimeMp4;
        }
        return null;
    }
}
