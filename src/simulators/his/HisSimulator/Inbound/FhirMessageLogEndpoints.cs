using HisSimulator.State;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HisSimulator.Inbound;

/// <summary>
/// Maps the read route for the FHIR message log shown by the ClientApp.
/// </summary>
public static class FhirMessageLogEndpoints
{
    /// <summary>
    /// Maps <c>GET /fhir-log</c>.
    /// </summary>
    /// <param name="app">The route builder to add the endpoint to.</param>
    /// <returns>The same <paramref name="app"/> instance, for chaining.</returns>
    public static IEndpointRouteBuilder MapFhirMessageLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/fhir-log", ListLog)
            .WithName("ListFhirMessageLog")
            .Produces<FhirMessageLogResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        return app;
    }

    /// <summary>
    /// Will list logged FHIR messages, newest first.
    /// </summary>
    /// <param name="direction">
    /// Optional filter (query string <c>direction</c>): <c>sent</c> or <c>received</c>, matched case-insensitively
    /// against <see cref="FhirMessageDirection"/>. Bound as text so an unknown value yields a descriptive 400.
    /// </param>
    /// <param name="caseRef">Optional filter (query string <c>caseRef</c>): a simulator-side case handle.</param>
    /// <returns>501 until implemented; then 200 with a <see cref="FhirMessageLogResponse"/>, or 400 for an unknown direction.</returns>
    private static ProblemHttpResult ListLog(string? direction, string? caseRef)
    {
        // TODO(WS-7): parse direction (Enum.TryParse, ignoreCase) and return SimulatorStore.GetLogEntries(direction, caseRef)
        // wrapped in FhirMessageLogResponse; answer 400 for an unknown direction.
        return StubResults.NotImplemented("FhirMessageLogEndpoints.ListLog");
    }
}
