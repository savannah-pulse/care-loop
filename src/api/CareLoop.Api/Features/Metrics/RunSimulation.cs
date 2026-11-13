using CareLoop.Api.Features.Common;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Contracts.Cases;
using CareLoop.Contracts.Common;
using CareLoop.Contracts.Exceptions;
using CareLoop.Contracts.Metrics;
using CareLoop.Contracts.Summaries;
using CareLoop.Domain.Cases;
using Microsoft.AspNetCore.Mvc;

namespace CareLoop.Api.Features.Metrics;

/// <summary>
/// Slice for <c>POST /metrics/simulation</c>: the with-and-without-escalation comparison.
/// </summary>
/// <remarks>
/// Will replay the same seeded synthetic cohort twice against an isolated in-memory engine with a controlled clock, once with deadlines and escalation active and once without, and report how many cases close or stall in each arm. It never touches the live case store and is an illustration of the coordination effect, not clinical evidence.
/// </remarks>
public static class RunSimulation
{
    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapRunSimulation(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/metrics/simulation", Handle)
            .WithName("RunSimulation")
            .WithTags("Metrics")
            .Produces<SimulationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);
        return routes;
    }

    /// <summary>
    /// Runs the escalation comparison on a simulated cohort.
    /// </summary>
    /// <param name="request">Which cohort scenario to replay and an optional seed.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>200 with both arms of the comparison, or 400 when the cohort scenario is unknown.</returns>
    public static IResult Handle(SimulationRequest request, CancellationToken ct)
    {
        // TODO(WS-10): replay the cohort through EventIngestor with InMemory storage and a fake clock, twice, and compare.
        return NotImplementedResults.For("WS10", "RunSimulation");
    }
}
