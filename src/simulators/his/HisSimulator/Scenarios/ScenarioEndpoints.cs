namespace HisSimulator.Scenarios;

/// <summary>Starts headless runs and exposes progress independently of the simulator UI.</summary>
public static class ScenarioEndpoints
{
    /// <summary>Maps scenario listing, asynchronous start and run progress.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapScenarioEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/scenarios", async (ScenarioLoader loader, CancellationToken ct) =>
            TypedResults.Ok(new ScenarioListResponse((await loader.LoadAllAsync(ct)).Select(s =>
                new ScenarioSummaryResponse(s.Name, s.Description, s.PathwayId, s.Cohort is not null,
                    s.Steps?.Count(step => step.Actor == ScenarioActor.His) ?? 0, s.Steps?.Count ?? 0, s.Expect)).ToArray())))
            .WithName("ListScenarios").WithSummary("Lists synthetic scenarios and their declared expectations.");
        app.MapPost("/scenarios/{name}/run", async (string name, ScenarioRunRequest? request, ScenarioRunner runner, CancellationToken ct) =>
        {
            var run = await runner.StartAsync(name, request, ct);
            return TypedResults.Accepted("/scenarios/runs/" + run.RunId, run);
        }).WithName("RunScenario").WithSummary("Starts HIS steps in the background using real-time pacing.")
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(422);
        app.MapGet("/scenarios/runs/{runId:guid}", IResult (Guid runId, ScenarioRunner runner, CancellationToken ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return runner.GetRun(runId) is { } run ? TypedResults.Ok(run) : TypedResults.NotFound();
        }).WithName("GetScenarioRun").Produces<State.ScenarioRun>().Produces(404);
        return app;
    }
}
