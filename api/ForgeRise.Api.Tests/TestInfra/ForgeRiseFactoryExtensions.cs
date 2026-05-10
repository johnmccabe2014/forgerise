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
        return factory;
    }
}

