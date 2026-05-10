using System.Linq;
using ForgeRise.Api.Data;
using ForgeRise.Api.Tests.TestInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ForgeRise.Api.Tests.Video;

/// <summary>
/// AppDbContext-level tests for the V1 video module: every new entity is
/// reachable via a DbSet, the model registers all 10 entity types, and
/// every entity's primary indexes lead with TeamId.
/// </summary>
public class VideoModelRegistrationTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public VideoModelRegistrationTests(ForgeRiseFactory factory) => _factory = factory;

    private static readonly string[] ExpectedClrNames =
    {
        "VideoUploadSession",
        "VideoAsset",
        "SessionVideoLink",
        "VideoTimelineEvent",
        "VideoClip",
        "VideoTag",
        "CoachVoiceNote",
        "TranscriptSegment",
        "AiVideoInsight",
        "HighlightCandidate",
    };

    [Fact]
    public void Model_registers_all_ten_video_entities()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var registered = db.Model.GetEntityTypes()
            .Select(t => t.ClrType.Name)
            .ToHashSet();

        foreach (var name in ExpectedClrNames)
        {
            Assert.Contains(name, registered);
        }
    }

    [Fact]
    public void Every_video_entity_has_a_TeamId_leading_index()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var name in ExpectedClrNames)
        {
            var entity = db.Model.GetEntityTypes()
                .First(t => t.ClrType.Name == name);

            var hasTeamLeadingIndex = entity.GetIndexes()
                .Any(ix => ix.Properties.Count > 0 && ix.Properties[0].Name == "TeamId");

            Assert.True(hasTeamLeadingIndex,
                $"{name} must have at least one index whose first column is TeamId.");
        }
    }
}
