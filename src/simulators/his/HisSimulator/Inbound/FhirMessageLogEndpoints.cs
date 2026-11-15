using HisSimulator.State;

namespace HisSimulator.Inbound;

/// <summary>Reads exact synthetic FHIR wire exchanges, including refusals.</summary>
public static class FhirMessageLogEndpoints
{
    /// <summary>Maps the filtered message-log read.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapFhirMessageLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/fhir-log", IResult (string? direction, string? caseRef, SimulatorStore store, CancellationToken ct) =>
        {
            ct.ThrowIfCancellationRequested();
            FhirMessageDirection? filter = direction?.ToLowerInvariant() switch
            {
                null => null, "sent" => FhirMessageDirection.Sent, "received" => FhirMessageDirection.Received,
                _ => throw new ArgumentException("Direction must be sent or received."),
            };
            return TypedResults.Ok(new FhirMessageLogResponse(store.GetLogEntries(filter, caseRef)));
        }).WithName("ListFhirMessageLog").WithSummary("Lists synthetic FHIR request and response bodies.")
            .Produces<FhirMessageLogResponse>().ProducesProblem(400);
        return app;
    }
}
