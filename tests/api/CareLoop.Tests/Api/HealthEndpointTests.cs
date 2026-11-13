using CareLoop.Api.Features.Common;
using CareLoop.Contracts.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CareLoop.Tests.Api;

/// <summary>
/// <c>GET /health</c> is the one route that works in every environment; Docker Compose and CI rely on it.
/// </summary>
public sealed class HealthEndpointTests
{
    [Fact]
    public void Health_endpoint_reports_ok_for_the_api()
    {
        var result = HealthEndpoint.Handle();

        var ok = Assert.IsType<Ok<HealthResponse>>(result);
        Assert.Equal("ok", ok.Value?.Status);
        Assert.Equal("careloop-api", ok.Value?.Service);
    }
}
