using System.Net;
using System.Net.Http.Json;

namespace CareLoop.Api.Tests;

public sealed class HealthEndpointTests(CareLoopApiFactory factory)
    : IClassFixture<CareLoopApiFactory>
{
    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", health?.Status);
    }

    private sealed record HealthResponse(string Status);
}
