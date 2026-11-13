namespace HisSimulator;

/// <summary>
/// Maps the liveness endpoint used by Docker Compose and by the ClientApp.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>The service name reported by <c>GET /health</c>.</summary>
    public const string ServiceName = "his-simulator";

    /// <summary>
    /// Maps <c>GET /health</c>, which returns 200 with a <see cref="HealthResponse"/> whenever the process is up.
    /// </summary>
    /// <param name="app">The route builder to add the endpoint to.</param>
    /// <returns>The same <paramref name="app"/> instance, for chaining.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok", ServiceName)))
            .WithName("GetHealth")
            .Produces<HealthResponse>(StatusCodes.Status200OK);

        return app;
    }
}
