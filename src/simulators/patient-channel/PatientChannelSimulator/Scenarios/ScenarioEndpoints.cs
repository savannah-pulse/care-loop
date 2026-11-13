using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// HTTP routes for listing and running scenarios.
/// </summary>
public static class ScenarioEndpoints
{
    /// <summary>
    /// Maps <c>GET /scenarios</c>, <c>POST /scenarios/{name}/run</c> and <c>GET /scenarios/runs/{runId}</c>.
    /// </summary>
    /// <param name="routes">The route builder to add the routes to.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapScenarioEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/scenarios").WithTags("Scenarios");

        group.MapGet("/", () =>
            {
                // TODO(WS-8): return ScenarioLoader.ListAsync mapped to ScenarioSummary.
                return NotImplementedResponse.For("GET /scenarios");
            })
            .WithName("ListScenarios")
            .WithSummary("Lists the scenarios available in the configured directory (SIMULATED).")
            .Produces<IReadOnlyList<ScenarioSummary>>()
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        group.MapPost("/{name}/run", (string name, RunScenarioRequest? request) =>
            {
                // TODO(WS-8): call ScenarioRunner.StartAsync; 202 with the run state, 404 for an unknown
                // scenario, 400 for a cohort scenario.
                return NotImplementedResponse.For("POST /scenarios/{name}/run");
            })
            .WithName("RunScenario")
            .WithSummary("Puts the patient behaviours of a scenario in force, so those patients react to CareLoop's notifications (SIMULATED).")
            .Produces<ScenarioRunState>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        group.MapGet("/runs/{runId}", (string runId) =>
            {
                // TODO(WS-8): return ScenarioRunner.GetRun(runId) or 404.
                return NotImplementedResponse.For("GET /scenarios/runs/{runId}");
            })
            .WithName("GetScenarioRun")
            .WithSummary("Reads a scenario run: its patients and how they have reacted so far (SIMULATED).")
            .Produces<ScenarioRunState>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        return routes;
    }
}
