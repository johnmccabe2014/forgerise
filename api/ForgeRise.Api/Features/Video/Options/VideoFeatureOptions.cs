namespace ForgeRise.Api.Features.Video.Options;

/// <summary>
/// Master flag for the Video Intelligence module. Default <c>false</c> in
/// every environment — the controller returns 404 when disabled, so the
/// surface is invisible to clients. Bound from configuration key
/// <c>Features:Video</c>.
/// </summary>
public sealed class VideoFeatureOptions
{
    public const string SectionName = "Features:Video";
    public bool Enabled { get; set; }
}
