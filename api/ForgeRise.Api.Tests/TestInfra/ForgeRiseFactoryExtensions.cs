namespace ForgeRise.Api.Tests.TestInfra;

/// <summary>
/// Test-only helpers to override factory configuration. Mutates the supplied
/// factory's <see cref="ForgeRiseFactory.ExtraConfig"/> and returns the same
/// instance — call BEFORE creating any client.
/// </summary>
public static class ForgeRiseFactoryExtensions
{
    public static ForgeRiseFactory WithVideoEnabled(this ForgeRiseFactory factory)
    {
        factory.ExtraConfig["Features:Video:Enabled"] = "true";
        // Even when the test never uploads, signing-options validation runs
        // at startup. Provide a deterministic 64-byte secret.
        factory.ExtraConfig["Features:Video:SigningSecret"] =
            "test-signing-secret-must-be-at-least-32-bytes-of-entropy-here-12";
        if (!factory.ExtraConfig.ContainsKey("Features:Video:Root"))
        {
            var root = Path.Combine(Path.GetTempPath(), $"forgerise-video-{Guid.NewGuid():n}");
            Directory.CreateDirectory(root);
            factory.ExtraConfig["Features:Video:Root"] = root;
        }
        return factory;
    }
}

