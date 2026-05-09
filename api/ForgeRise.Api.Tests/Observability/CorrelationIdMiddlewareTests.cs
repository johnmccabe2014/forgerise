using System.Net;
using ForgeRise.Api.Tests.TestInfra;
using Xunit;

namespace ForgeRise.Api.Tests.Observability;

public class CorrelationIdMiddlewareTests : IClassFixture<ForgeRiseFactory>
{
    private readonly ForgeRiseFactory _factory;

    public CorrelationIdMiddlewareTests(ForgeRiseFactory factory) => _factory = factory;

    [Fact]
    public async Task Generates_correlation_id_when_absent()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        var id = values!.Single();
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.InRange(id.Length, 8, 128);
    }

    [Fact]
    public async Task Echoes_valid_inbound_correlation_id()
    {
        var client = _factory.CreateClient();
        var inbound = "req_abc-123_456";

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", inbound);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(inbound, response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Replaces_invalid_inbound_correlation_id()
    {
        var client = _factory.CreateClient();
        var bad = "tiny"; // too short to satisfy the 8-128 char guard.

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", bad);

        var response = await client.SendAsync(request);

        var echoed = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.NotEqual(bad, echoed);
        Assert.InRange(echoed.Length, 8, 128);
    }
}
