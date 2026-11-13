using PatientChannelSimulator.Outbound;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Route the ClientApp uses for the patient case-status view.
/// </summary>
public static class CaseStatusEndpoints
{
    /// <summary>
    /// Maps <c>GET /api/case-status/{taskId}</c>.
    /// </summary>
    /// <param name="routes">The route builder to add the route to.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapCaseStatusEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/case-status/{taskId}", (string taskId) =>
            {
                // TODO(WS-8): call CaseStatusReader.ReadAsync(taskId); 200 with the status, 404 when CareLoop
                // has no such Task, 502 when CareLoop cannot be reached.
                return NotImplementedResponse.For("GET /api/case-status/{taskId}");
            })
            .WithName("GetCaseStatus")
            .WithSummary("Reads the patient-facing status of a case from CareLoop's case Task (SIMULATED).")
            .WithTags("Case status")
            .Produces<PatientCaseStatus>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status502BadGateway)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        return routes;
    }
}
