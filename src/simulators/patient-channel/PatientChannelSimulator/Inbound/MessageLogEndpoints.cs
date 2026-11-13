using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Route the ClientApp uses to show the FHIR message log.
/// </summary>
public static class MessageLogEndpoints
{
    /// <summary>
    /// Maps <c>GET /api/fhir-log</c>.
    /// </summary>
    /// <param name="routes">The route builder to add the route to.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMessageLogEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/fhir-log", () =>
            {
                // TODO(WS-8): return SimulatorStore.GetLogEntries().
                return NotImplementedResponse.For("GET /api/fhir-log");
            })
            .WithName("ListFhirLog")
            .WithSummary("Lists every FHIR exchange between this simulator and CareLoop, newest first (SIMULATED).")
            .WithTags("FHIR message log")
            .Produces<IReadOnlyList<FhirMessageLogEntry>>()
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        return routes;
    }
}
