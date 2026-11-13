using System.Text.Json;
using CareLoop.Api.Features.Events;
using CareLoop.Api.Features.Fhir.Outbound;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Events;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace CareLoop.Api.Features.Fhir;

/// <summary>
/// The FHIR boundary: the only door through which systems of record talk to CareLoop.
/// </summary>
/// <remarks>
/// Hospital systems (the HIS simulator in development) send and read FHIR R4
/// resources here. This slice contains no business logic: it parses the resource, picks the
/// <see cref="IFhirInboundMapper"/> for its type, and hands the resulting envelopes to the event
/// ingestor. There is deliberately no public <c>POST /events</c>.
/// <para>
/// The sending system identifies itself in <c>meta.source</c>; the value is the source system identifier,
/// the same key it has under <c>ExternalSystems:Systems</c>, and is what replies are routed back to.
/// </para>
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
            .Produces<JsonElement>(StatusCodes.Status200OK, FhirJson);

        fhir.MapGet("/Task/{id}", GetTask)
            .WithName("GetFhirTask")
            .Produces<JsonElement>(StatusCodes.Status200OK, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status404NotFound, FhirJson);

        fhir.MapPost("/{resourceType}", CreateResource)
            .WithName("CreateFhirResource")
            .Accepts<JsonElement>(FhirJson, "application/json")
            .Produces<JsonElement>(StatusCodes.Status201Created, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status200OK, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status400BadRequest, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status422UnprocessableEntity, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status503ServiceUnavailable, FhirJson);

        fhir.MapPut("/{resourceType}/{id}", UpdateResource)
            .WithName("UpdateFhirResource")
            .Accepts<JsonElement>(FhirJson, "application/json")
            .Produces<JsonElement>(StatusCodes.Status200OK, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status400BadRequest, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status422UnprocessableEntity, FhirJson)
            .Produces<JsonElement>(StatusCodes.Status503ServiceUnavailable, FhirJson);

        return routes;
    }

    /// <summary>
    /// Accepts a new FHIR resource from an external system.
    /// </summary>
    /// <param name="resourceType">The FHIR resource type in the URL, for example <c>DiagnosticReport</c>; must match the body.</param>
    /// <param name="request">The HTTP request; its body is the FHIR R4 resource as JSON.</param>
    /// <param name="mappers">The registered inbound mappers, one per supported resource type.</param>
    /// <param name="ingestor">The single intake pipeline.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// 201 with the stored resource when the event was applied; 200 when it was a duplicate (same
    /// identifier and version) or represents no tracked transition; 400 with an <c>OperationOutcome</c> when the body
    /// is not valid FHIR, names no source system, or no mapper exists for the type; 422 with an <c>OperationOutcome</c>
    /// when a domain rule refused the event; 503 when a resource it refers to could not be read from the source.
    /// </returns>
    public static Task<IResult> CreateResource(
        string resourceType,
        HttpRequest request,
        IEnumerable<IFhirInboundMapper> mappers,
        EventIngestor ingestor,
        CancellationToken ct) =>
        AcceptAsync(resourceType, id: null, request, mappers, ingestor, ct);

    /// <summary>
    /// Accepts a new version of an existing FHIR resource from an external system.
    /// </summary>
    /// <param name="resourceType">The FHIR resource type in the URL; must match the body.</param>
    /// <param name="id">The logical identifier of the resource; must match the body.</param>
    /// <param name="request">The HTTP request; its body is the FHIR R4 resource as JSON.</param>
    /// <param name="mappers">The registered inbound mappers.</param>
    /// <param name="ingestor">The single intake pipeline.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// 200 with the resource when the new state was applied or was a duplicate; 400, 422 or 503 with an
    /// <c>OperationOutcome</c> as for <see cref="CreateResource"/>.
    /// </returns>
    /// <remarks>An update is state, not an event: the mapper derives the transition and the resource version makes a resend idempotent.</remarks>
    public static Task<IResult> UpdateResource(
        string resourceType,
        string id,
        HttpRequest request,
        IEnumerable<IFhirInboundMapper> mappers,
        EventIngestor ingestor,
        CancellationToken ct) =>
        AcceptAsync(resourceType, id, request, mappers, ingestor, ct);

    /// <summary>
    /// Returns the current status of a case as a FHIR <c>Task</c>; also feeds the patient view.
    /// </summary>
    /// <param name="id">CareLoop's case identifier, used as the logical identifier of the case <c>Task</c>.</param>
    /// <param name="db">The coordination store, read with a no-tracking query.</param>
    /// <param name="caseTasks">Builds the case <c>Task</c>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>200 with the case <c>Task</c>, or 404 with an <c>OperationOutcome</c> when no case has that identifier.</returns>
    /// <remarks>
    /// Field mapping: handoff status to <c>Task.status</c>; stage to <c>Task.businessStatus</c>; owner to
    /// <c>Task.owner</c>; who handed over to <c>Task.requester</c>; the result or referral to <c>Task.focus</c>;
    /// patient to <c>Task.for</c>; deadline to <c>Task.restriction.period</c>; rejection or closure reason to
    /// <c>Task.statusReason</c>.
    /// </remarks>
    public static async Task<IResult> GetTask(string id, CareLoopDbContext db, CaseTaskMapper caseTasks, CancellationToken ct)
    {
        var @case = Guid.TryParse(id, out var caseId)
            ? await db.Cases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == caseId, ct)
            : null;

        return @case is null
            ? Outcome(StatusCodes.Status404NotFound, OperationOutcome.IssueType.NotFound, $"No case has the identifier '{id}'.")
            : new FhirResourceResult(StatusCodes.Status200OK, caseTasks.Map(@case));
    }

    private static async Task<IResult> AcceptAsync(
        string resourceType,
        string? id,
        HttpRequest request,
        IEnumerable<IFhirInboundMapper> mappers,
        EventIngestor ingestor,
        CancellationToken ct)
    {
        Resource resource;
        try
        {
            using var reader = new StreamReader(request.Body);
            resource = FhirJsonCodec.Deserialize(await reader.ReadToEndAsync(ct));
        }
        catch (Exception ex) when (ex is DeserializationFailedException or JsonException)
        {
            return Outcome(StatusCodes.Status400BadRequest, OperationOutcome.IssueType.Structure, $"The body is not a valid FHIR R4 resource: {ex.Message}");
        }

        if (!string.Equals(resource.TypeName, resourceType, StringComparison.Ordinal))
        {
            return Outcome(StatusCodes.Status400BadRequest, OperationOutcome.IssueType.Invalid, $"The URL names {resourceType} but the body is a {resource.TypeName}.");
        }

        if (id is not null && !string.Equals(resource.Id, id, StringComparison.Ordinal))
        {
            return Outcome(StatusCodes.Status400BadRequest, OperationOutcome.IssueType.Invalid, $"The URL names id '{id}' but the body has id '{resource.Id}'.");
        }

        var sourceSystem = resource.Meta?.Source;
        if (string.IsNullOrWhiteSpace(sourceSystem))
        {
            return Outcome(StatusCodes.Status400BadRequest, OperationOutcome.IssueType.Required, "meta.source must name the sending system.");
        }

        var mapper = mappers.FirstOrDefault(m => string.Equals(m.ResourceType, resource.TypeName, StringComparison.Ordinal));
        if (mapper is null)
        {
            return Outcome(StatusCodes.Status400BadRequest, OperationOutcome.IssueType.NotSupported, $"CareLoop does not accept {resource.TypeName} resources.");
        }

        IReadOnlyList<EventEnvelope> envelopes;
        try
        {
            envelopes = await mapper.MapAsync(resource, sourceSystem, ct);
        }
        catch (FormatException ex)
        {
            return Outcome(StatusCodes.Status400BadRequest, OperationOutcome.IssueType.Required, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return Outcome(StatusCodes.Status503ServiceUnavailable, OperationOutcome.IssueType.Transient, $"A resource the {resource.TypeName} refers to could not be read from '{sourceSystem}': {ex.Message}");
        }

        var applied = false;
        foreach (var envelope in envelopes)
        {
            var result = await ingestor.IngestAsync(envelope, ct);
            switch (result.Outcome)
            {
                case IngestOutcome.Invalid:
                    return Outcome(StatusCodes.Status400BadRequest, OperationOutcome.IssueType.Invalid, result.Reason ?? "The event is not valid.");
                case IngestOutcome.Rejected:
                    return Outcome(StatusCodes.Status422UnprocessableEntity, OperationOutcome.IssueType.BusinessRule, result.Reason ?? "A rule refused the event.");
                case IngestOutcome.Applied:
                    applied = true;
                    break;
            }
        }

        var created = applied && id is null;
        return new FhirResourceResult(
            created ? StatusCodes.Status201Created : StatusCodes.Status200OK,
            resource,
            created && resource.Id is not null ? $"/fhir/{resource.TypeName}/{resource.Id}" : null);
    }

    private static FhirResourceResult Outcome(int status, OperationOutcome.IssueType code, string diagnostics) =>
        new(status, new OperationOutcome
        {
            Issue =
            [
                new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = code,
                    Diagnostics = diagnostics,
                },
            ],
        });
}

/// <summary>
/// Writes a FHIR resource as <c>application/fhir+json</c> with a status code and, for a create, a location.
/// </summary>
/// <param name="statusCode">The HTTP status code.</param>
/// <param name="resource">The resource to write.</param>
/// <param name="location">The <c>Location</c> header, or <see langword="null"/> for none.</param>
public sealed class FhirResourceResult(int statusCode, Resource resource, string? location = null) : IResult, IStatusCodeHttpResult
{
    /// <summary>The resource written as the body.</summary>
    public Resource Resource { get; } = resource;

    /// <summary>The <c>Location</c> header, when the resource was created.</summary>
    public string? Location { get; } = location;

    /// <inheritdoc/>
    public int StatusCode { get; } = statusCode;

    /// <inheritdoc/>
    int? IStatusCodeHttpResult.StatusCode => StatusCode;

    /// <inheritdoc/>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = StatusCode;
        if (Location is not null)
        {
            httpContext.Response.Headers.Location = Location;
        }

        httpContext.Response.ContentType = FhirEndpoints.FhirJson + "; charset=utf-8";
        return httpContext.Response.WriteAsync(FhirJsonCodec.Serialize(Resource), httpContext.RequestAborted);
    }
}
