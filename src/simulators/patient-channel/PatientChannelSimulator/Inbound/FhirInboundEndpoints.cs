namespace PatientChannelSimulator.Inbound;

/// <summary>
/// The simulator's own FHIR endpoint, called by CareLoop's simulator notification provider.
/// </summary>
public static class FhirInboundEndpoints
{
    /// <summary>FHIR JSON media type used for request and response bodies.</summary>
    public const string FhirJson = "application/fhir+json";

    /// <summary>
    /// Maps <c>POST /fhir/{resourceType}</c>. Only <c>CommunicationRequest</c> is supported.
    /// </summary>
    /// <param name="routes">The route builder to add the route to.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapFhirInboundEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/fhir/{resourceType}", (string resourceType) =>
            {
                // TODO(WS-8): read the raw body; for CommunicationRequest call CommunicationRequestReceiver
                // and answer 201 with the stored resource as application/fhir+json; answer 400 with an
                // OperationOutcome for an unsupported resource type or an unparseable body.
                return NotImplementedResponse.For("POST /fhir/{resourceType}");
            })
            .WithName("ReceiveFhirResource")
            .WithSummary("Receives a FHIR R4 resource from CareLoop; CommunicationRequest lands in the simulated patient inbox (SIMULATED).")
            .WithTags("FHIR inbound")
            .Accepts<string>(FhirJson, "application/json")
            .Produces<string>(StatusCodes.Status201Created, FhirJson)
            .Produces<string>(StatusCodes.Status400BadRequest, FhirJson)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        return routes;
    }
}
