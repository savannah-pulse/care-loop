using Hl7.Fhir.Model;
using PatientChannelSimulator.Outbound;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// The simulator's own FHIR endpoint, called by CareLoop's simulator notification provider.
/// </summary>
public static class FhirInboundEndpoints
{
    /// <summary>FHIR JSON media type used for request and response bodies.</summary>
    public const string FhirJson = Outbound.FhirJson.MediaType;

    /// <summary>
    /// Maps <c>POST /fhir/{resourceType}</c>. Only <c>CommunicationRequest</c> is supported.
    /// </summary>
    /// <param name="routes">The route builder to add the route to.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapFhirInboundEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/fhir/{resourceType}", async (string resourceType, HttpRequest request, CommunicationRequestReceiver receiver, CancellationToken ct) =>
            {
                if (!string.Equals(resourceType, "CommunicationRequest", StringComparison.Ordinal))
                {
                    return FhirOutcome.Error(
                        StatusCodes.Status400BadRequest,
                        OperationOutcome.IssueType.NotSupported,
                        "The patient-channel simulator only accepts CommunicationRequest.");
                }

                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(ct);

                try
                {
                    var received = await receiver.ReceiveAsync(body, ct);

                    // 201 for a new message, 200 for a resend: either way CareLoop's send succeeded.
                    request.HttpContext.Response.Headers.Location = $"/fhir/CommunicationRequest/{Uri.EscapeDataString(received.Message.CommunicationRequestId)}";
                    return Results.Text(
                        body,
                        FhirJson,
                        statusCode: received.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK);
                }
                catch (FormatException ex)
                {
                    return FhirOutcome.Error(StatusCodes.Status400BadRequest, OperationOutcome.IssueType.Invalid, ex.Message);
                }
                catch (Exception ex) when (ex is HttpRequestException or CareLoopNotConfiguredException)
                {
                    // The message is in the inbox but its delivery outcome could not be reported.
                    // A failure answer lets CareLoop's outbox send again, which retries the report.
                    return FhirOutcome.Error(
                        StatusCodes.Status502BadGateway,
                        OperationOutcome.IssueType.Transient,
                        $"The message was received but its delivery outcome could not be reported to CareLoop: {ex.Message}");
                }
            })
            .WithName("ReceiveFhirResource")
            .WithSummary("Receives a FHIR R4 resource from CareLoop; CommunicationRequest lands in the simulated patient inbox (SIMULATED).")
            .WithTags("FHIR inbound")
            .Accepts<string>(FhirJson, "application/json")
            .Produces<string>(StatusCodes.Status201Created, FhirJson)
            .Produces<string>(StatusCodes.Status200OK, FhirJson)
            .Produces<string>(StatusCodes.Status400BadRequest, FhirJson)
            .Produces<string>(StatusCodes.Status502BadGateway, FhirJson);

        return routes;
    }
}
