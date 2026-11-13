using HisSimulator.State;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HisSimulator.Scenarios;

/// <summary>
/// Maps the scenario runner routes used by the ClientApp and by end-to-end tests.
/// </summary>
public static class ScenarioEndpoints
{
    /// <summary>
    /// Maps <c>GET /scenarios</c>, <c>POST /scenarios/{name}/run</c> and <c>GET /scenarios/runs/{runId}</c>.
    /// </summary>
    /// <param name="app">The route builder to add the endpoints to.</param>
    /// <returns>The same <paramref name="app"/> instance, for chaining.</returns>
    public static IEndpointRouteBuilder MapScenarioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/scenarios");

        group.MapGet("/", ListScenarios)
            .WithName("ListScenarios")
            .Produces<ScenarioListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPost("/{name}/run", RunScenario)
            .WithName("RunScenario")
            .Accepts<ScenarioRunRequest>("application/json")
            .Produces<ScenarioRun>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapGet("/runs/{runId:guid}", GetScenarioRun)
            .WithName("GetScenarioRun")
            .Produces<ScenarioRun>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        return app;
    }

    /// <summary>
    /// Will list every scenario in the configured directory as a <see cref="ScenarioListResponse"/>.
    /// </summary>
    /// <returns>501 until implemented; then 200 with the list.</returns>
    private static ProblemHttpResult ListScenarios()
    {
        // TODO(WS-7): call ScenarioLoader.LoadAllAsync and project each definition to ScenarioSummaryResponse.
        return StubResults.NotImplemented("ScenarioEndpoints.ListScenarios");
    }

    /// <summary>
    /// Will start a run of the named scenario and answer 202 with the initial <see cref="ScenarioRun"/>.
    /// </summary>
    /// <param name="name">Scenario key from the route.</param>
    /// <param name="request">Optional start instant, pacing and run key.</param>
    /// <returns>501 until implemented; then 202, 400 for an invalid name or file, or 404 for an unknown scenario.</returns>
    private static ProblemHttpResult RunScenario(string name, ScenarioRunRequest? request)
    {
        // TODO(WS-7): call ScenarioRunner.StartAsync; map ScenarioNotFoundException to 404 and
        // ArgumentException/InvalidDataException to 400; return 202 with a Location of /scenarios/runs/{runId}.
        return StubResults.NotImplemented("ScenarioEndpoints.RunScenario");
    }

    /// <summary>
    /// Will return the latest state of a run so the ClientApp can poll progress.
    /// </summary>
    /// <param name="runId">Run identifier from the route.</param>
    /// <returns>501 until implemented; then 200 with the run, or 404 if it is unknown.</returns>
    private static ProblemHttpResult GetScenarioRun(Guid runId)
    {
        // TODO(WS-7): call ScenarioRunner.GetRun and return 404 when it yields null.
        return StubResults.NotImplemented("ScenarioEndpoints.GetScenarioRun");
    }
}
