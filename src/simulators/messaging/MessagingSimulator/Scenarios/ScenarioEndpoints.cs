using MessagingSimulator.State;

namespace MessagingSimulator.Scenarios;

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

        group.MapGet("/", async (ScenarioLoader loader, CancellationToken ct) =>
            {
                try
                {
                    var entries = await loader.ListEntriesAsync(ct);
                    return Results.Ok(entries.Select(e => e.Scenario is { } s
                        ? new ScenarioSummary(
                            s.Name,
                            s.Description,
                            s.PathwayId,
                            IsCohort: s.Cohort is not null,
                            PatientCount: s.Cohort is null ? s.Patients?.Count ?? 0 : 0)
                        : new ScenarioSummary(e.Name, string.Empty, string.Empty, IsCohort: false, PatientCount: 0, Problem: e.Problem)).ToList());
                }
                catch (Exception ex) when (ex is DirectoryNotFoundException or InvalidDataException)
                {
                    return Unreadable(ex);
                }
            })
            .WithName("ListScenarios")
            .WithSummary("Lists the scenarios available in the configured directory (SIMULATED).")
            .Produces<IReadOnlyList<ScenarioSummary>>()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/{name}/run", async (string name, RunScenarioRequest? request, ScenarioRunner runner, CancellationToken ct) =>
            {
                try
                {
                    var run = await runner.StartAsync(name, request ?? new RunScenarioRequest(), ct);
                    return run is null
                        ? Results.NotFound()
                        : Results.Accepted($"/scenarios/runs/{run.RunId}", run);
                }
                catch (ArgumentException ex)
                {
                    // Covers a name that is not a scenario key and a pacing that is out of range.
                    return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "The run request is not valid.");
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "This scenario cannot be run by the messaging simulator.");
                }
                catch (Exception ex) when (ex is DirectoryNotFoundException or InvalidDataException)
                {
                    return Unreadable(ex);
                }
            })
            .WithName("RunScenario")
            .WithSummary("Puts the patient behaviours of a scenario in force, so those patients react to CareLoop's notifications (SIMULATED).")
            .Produces<ScenarioRunState>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/runs/{runId}", (string runId, ScenarioRunner runner) =>
                runner.GetRun(runId) is { } run ? Results.Ok(run) : Results.NotFound())
            .WithName("GetScenarioRun")
            .WithSummary("Reads a scenario run: its patients and how they have reacted so far (SIMULATED).")
            .Produces<ScenarioRunState>()
            .Produces(StatusCodes.Status404NotFound);

        return routes;
    }

    private static IResult Unreadable(Exception ex) =>
        Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError, title: "The scenario files could not be read.");
}
