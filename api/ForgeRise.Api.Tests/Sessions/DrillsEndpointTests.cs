using System.Net;
using System.Net.Http.Json;
using ForgeRise.Api.Sessions.Contracts;
using ForgeRise.Api.Tests.TestInfra;
using Xunit;

namespace ForgeRise.Api.Tests.Sessions;

public class DrillsEndpointTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;
    public DrillsEndpointTests(ForgeRiseFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthedAsync()
    {
        var client = _factory.CreateDefaultClient(new CookieJarHandler());
        var r = await client.PostAsJsonAsync("/auth/register", new
        {
            email = $"drills-{Guid.NewGuid():n}@example.com",
            password = "Correct horse battery staple",
            displayName = "drills-tester",
        });
        r.EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Unauthenticated_requests_are_rejected()
    {
        var client = _factory.CreateDefaultClient();
        var resp = await client.GetAsync("/drills");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task List_returns_full_catalogue_with_enriched_fields()
    {
        var client = await AuthedAsync();
        var drills = await client.GetFromJsonAsync<List<DrillDto>>("/drills");
        Assert.NotNull(drills);
        Assert.True(drills!.Count >= 10);
        // Every drill must have populated coaching content — that's the whole
        // point of this slice; we never want to ship empty cards to a coach.
        Assert.All(drills, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.LongDescription));
            Assert.NotEmpty(d.CoachingCues);
            Assert.NotEmpty(d.Equipment);
            Assert.False(string.IsNullOrWhiteSpace(d.WhatItMeans));
            Assert.False(string.IsNullOrWhiteSpace(d.DiagramKey));
        });
    }

    [Fact]
    public async Task Get_returns_a_specific_drill_with_glossary_matches()
    {
        var client = await AuthedAsync();
        var drill = await client.GetFromJsonAsync<DrillDto>("/drills/conditioned-game");
        Assert.NotNull(drill);
        Assert.Equal("conditioned-game", drill!.Id);
        // The conditioned-game drill description mentions "constraints" and
        // "small-sided" — both should be picked up by the glossary.
        Assert.Contains(drill.Glossary, g => g.Term == "constraint");
    }

    [Fact]
    public async Task Get_unknown_id_returns_404()
    {
        var client = await AuthedAsync();
        var resp = await client.GetAsync("/drills/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
