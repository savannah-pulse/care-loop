using System.Text.Json;
using CareLoop.Api.Features.Common;
using CareLoop.Contracts.Common;

namespace CareLoop.Api.Features.Fhir;

/// <summary>
/// The FHIR boundary: the only door through which systems of record talk to CareLoop.
/// </summary>
/// <remarks>
/// Hospital systems (the HIS simulator in development) send and read FHIR R4
/// resources here. This slice contains no business logic: it parses the resource, picks the
/// <see cref="IFhirInboundMapper"/> for its type, and hands the resulting envelopes to the event
/// ingestor. There is deliberately no public <c>POST /events</c>.
/// </remarks>
public static class FhirEndpoints
{
    /// <summary>The media type FHIR JSON resources are exchanged in.</summary>
    public const string FhirJson = "application/fhir+json";

    /// <summary>
    /// Maps the FHIR routes.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapFhirEndpoints(this IEndpointRouteBuilder routes)
    {
        var fhir = routes.MapGroup("/fhir").WithTags("FHIR");

        fhir.MapGet("/metadata", GetCapabilityStatement.Handle)
            .WithName("GetFhirCapabilityStatement")
            .Produces<JsonElement>(StatusCodes.Status200OK, FhirJson)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        fhir.MapGet("/Task/{id}", GetTask)
            .WithName("GetFhirTask")
            .Produces<JsonElement>(StatusCodes.Status200OK, FhirJson)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        fhir.MapPost("/{resourceType}", CreateResource)
            .WithName("CreateFhirResource")
            .Accepts<JsonElement>(FhirJson, "application/json")
            .Produces<JsonElement>(StatusCodes.Status201Created, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status200OK, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status400BadRequest, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status422UnprocessableEntity, FhirJson)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        fhir.MapPut("/{resourceType}/{id}", UpdateResource)
            .WithName("UpdateFhirResource")
            .Accepts<JsonElement>(FhirJson, "application/json")
            .Produces<JsonElement>(StatusCodes.Status200OK, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status400BadRequest, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status422UnprocessableEntity, FhirJson)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        return routes;
    }

    /// <summary>
    /// Accepts a new FHIR resource from an external system.
    /// </summary>
    /// <param name="resourceType">The FHIR resource type in the URL, for example <c>DiagnosticReport</c>; must match the body.</param>
    /// <param name="body">The FHIR R4 resource as JSON.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// 201 with the stored resource when the event was applied; 200 when it was a duplicate (same
    /// identifier and version); 400 with an <c>OperationOutcome</c> when the body is not valid FHIR or no
    /// mapper exists for the type; 422 with an <c>OperationOutcome</c> when a domain rule refused the event.
    /// </returns>
    public static IResult CreateResource(string resourceType, JsonElement body, CancellationToken ct)
    {
        // TODO(WS-3): parse with the Firely parser, select IFhirInboundMapper by resource type,
        // pass each envelope to EventIngestor and translate the IngestResult to a FHIR response.
        return NotImplementedResults.For("WS3", "FhirEndpoints.CreateResource");
    }

    /// <summary>
    /// Accepts a new version of an existing FHIR resource from an external system.
    /// </summary>
    /// <param name="resourceType">The FHIR resource type in the URL; must match the body.</param>
    /// <param name="id">The logical identifier of the resource; must match the body.</param>
    /// <param name="body">The FHIR R4 resource as JSON.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// 200 with the resource when the new state was applied or was a duplicate; 400 or 422 with an
    /// <c>OperationOutcome</c> as for <see cref="CreateResource"/>.
    /// </returns>
    /// <remarks>An update is state, not an event: the mapper derives the transition and the resource version makes a resend idempotent.</remarks>
    public static IResult UpdateResource(string resourceType, string id, JsonElement body, CancellationToken ct)
    {
        // TODO(WS-3): same pipeline as CreateResource, checking that id matches the body.
        return NotImplementedResults.For("WS3", "FhirEndpoints.UpdateResource");
    }

    /// <summary>
    /// Returns the current status of a case as a FHIR <c>Task</c>; also feeds the patient view.
    /// </summary>
    /// <param name="id">CareLoop's case identifier, used as the logical identifier of the case <c>Task</c>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>200 with the case <c>Task</c>, or 404 when no case has that identifier.</returns>
    /// <remarks>
    /// Field mapping: handoff status to <c>Task.status</c>; stage to <c>Task.businessStatus</c>; owner to
    /// <c>Task.owner</c>; who handed over to <c>Task.requester</c>; the result or referral to <c>Task.focus</c>;
    /// patient to <c>Task.for</c>; deadline to <c>Task.restriction.period</c>; rejection or closure reason to
    /// <c>Task.statusReason</c>.
    /// </remarks>
    public static IResult GetTask(string id, CancellationToken ct)
    {
        // TODO(WS-3): load the case with a no-tracking query and map it with Outbound.CaseTaskMapper.
        return NotImplementedResults.For("WS3", "FhirEndpoints.GetTask");
    }
}
