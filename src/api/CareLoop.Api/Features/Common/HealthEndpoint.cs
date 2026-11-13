using CareLoop.Contracts.Common;

namespace CareLoop.Api.Features.Common;

/// <summary>
/// Slice for <c>GET /health</c>: tells Docker Compose and operators that the API process is up.
/// </summary>
public static class HealthEndpoint
{
    /// <summary>
    /// Maps the health route.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", Handle)
            .WithName("GetHealth")
            .WithTags("Health")
            .Produces<HealthResponse>();

        return routes;
    }

    /// <summary>
    /// Reports that the service is running.
    /// </summary>
    /// <returns>200 with the service name.</returns>
    /// <remarks>A liveness check only: it does not touch the database, so it stays green while a dependency is starting.</remarks>
    public static IResult Handle() =>
        TypedResults.Ok(new HealthResponse { Status = "ok", Service = "careloop-api" });
}
