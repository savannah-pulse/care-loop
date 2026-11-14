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
        routes.MapGet("/api/case-status/{taskId}", async (string taskId, CaseStatusReader reader, CancellationToken ct) =>
            {
                try
                {
                    return await reader.ReadAsync(taskId, ct) is { } status ? Results.Ok(status) : Results.NotFound();
                }
                catch (HttpRequestException ex)
                {
                    return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "CareLoop could not be reached.");
                }
                catch (FormatException ex)
                {
                    return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "CareLoop's answer was not a FHIR Task.");
                }
            })
            .WithName("GetCaseStatus")
            .WithSummary("Reads the patient-facing status of a case from CareLoop's case Task (SIMULATED).")
            .WithTags("Case status")
            .Produces<PatientCaseStatus>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return routes;
    }
}
