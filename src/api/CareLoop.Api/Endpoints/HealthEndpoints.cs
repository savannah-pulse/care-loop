namespace CareLoop.Api.Endpoints;

public sealed class HealthEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app) =>
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("HealthCheck");
}
