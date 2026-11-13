using Microsoft.AspNetCore.Http.HttpResults;

namespace HisSimulator.Inbound;

/// <summary>
/// Maps the simulated HIS's own FHIR endpoints, which CareLoop's <c>FhirPublisher</c> sends to.
/// </summary>
/// <remarks>
/// The request and response bodies are FHIR R4 JSON (<c>application/fhir+json</c>), parsed by the Firely
/// SDK rather than bound to a C# record, so the handlers read the raw body.
/// </remarks>
public static class FhirInboundEndpoints
{
    /// <summary>Media type of FHIR JSON bodies.</summary>
    public const string FhirJson = "application/fhir+json";

    /// <summary>
    /// Maps <c>POST /fhir/{resourceType}</c> and <c>PUT /fhir/{resourceType}/{id}</c>.
    /// </summary>
    /// <param name="app">The route builder to add the endpoints to.</param>
    /// <returns>The same <paramref name="app"/> instance, for chaining.</returns>
    public static IEndpointRouteBuilder MapFhirInboundEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/fhir");

        group.MapPost("/{resourceType}", CreateResource)
            .WithName("ReceiveFhirResource")
            .Accepts<string>(FhirJson, "application/json")
            .Produces<string>(StatusCodes.Status201Created, FhirJson)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPut("/{resourceType}/{id}", UpdateResource)
            .WithName("ReceiveFhirResourceUpdate")
            .Accepts<string>(FhirJson, "application/json")
            .Produces<string>(StatusCodes.Status200OK, FhirJson)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        return app;
    }

    /// <summary>
    /// Will receive a new resource from CareLoop: an escalation, handoff or call-request <c>Task</c>,
    /// a handoff-summary <c>DocumentReference</c> or a staff notification.
    /// </summary>
    /// <param name="resourceType">Resource type from the route.</param>
    /// <param name="request">The HTTP request whose body is the FHIR JSON.</param>
    /// <returns>
    /// 501 until implemented; then 201 with the stored resource, 400 for malformed FHIR or a type mismatch,
    /// or 422 for a resource type the simulated HIS does not accept.
    /// </returns>
    private static ProblemHttpResult CreateResource(string resourceType, HttpRequest request)
    {
        // TODO(WS-7): read the body, call InboundFhirReceiver.ReceiveAsync("POST", ...), return 201 with a
        // Location header and the stored resource; map FormatException/ArgumentException to 400 and
        // NotSupportedException to 422.
        return StubResults.NotImplemented("FhirInboundEndpoints.CreateResource");
    }

    /// <summary>
    /// Will receive a new state of a resource CareLoop sent earlier, for example a <c>Task</c> update after a rejection.
    /// </summary>
    /// <param name="resourceType">Resource type from the route.</param>
    /// <param name="id">Logical id from the route.</param>
    /// <param name="request">The HTTP request whose body is the FHIR JSON.</param>
    /// <returns>501 until implemented; then 200 with the stored resource, or 400/422 as for a create.</returns>
    private static ProblemHttpResult UpdateResource(string resourceType, string id, HttpRequest request)
    {
        // TODO(WS-7): as CreateResource but with "PUT" and the route id; return 200.
        return StubResults.NotImplemented("FhirInboundEndpoints.UpdateResource");
    }
}
