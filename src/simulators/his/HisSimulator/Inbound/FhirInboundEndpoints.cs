using System.Text.Json;

namespace HisSimulator.Inbound;

/// <summary>Receives FHIR from CareLoop and never acknowledges or accepts on receipt.</summary>
public static class FhirInboundEndpoints
{
    /// <summary>FHIR R4 JSON media type.</summary>
    public const string FhirJson = "application/fhir+json";

    /// <summary>Maps POST and id-checked PUT intake.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapFhirInboundEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/fhir/{resourceType}", async (string resourceType, HttpRequest request, InboundFhirReceiver receiver, CancellationToken ct) =>
            await Receive(resourceType, null, request, receiver, ct))
            .WithName("ReceiveFhirResource").WithSummary("Receives a synthetic HIS inbox resource from CareLoop.")
            .Accepts<JsonElement>(FhirJson, "application/json").Produces<JsonElement>(201, FhirJson).ProducesProblem(400).ProducesProblem(422);
        app.MapPut("/fhir/{resourceType}/{id}", async (string resourceType, string id, HttpRequest request, InboundFhirReceiver receiver, CancellationToken ct) =>
            await Receive(resourceType, id, request, receiver, ct))
            .WithName("ReceiveFhirResourceUpdate").WithSummary("Idempotently files an updated inbox resource.")
            .Accepts<JsonElement>(FhirJson, "application/json").Produces<JsonElement>(200, FhirJson).ProducesProblem(400).ProducesProblem(422);
        return app;
    }

    private static async Task<IResult> Receive(string type, string? id, HttpRequest request, InboundFhirReceiver receiver, CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var received = await receiver.ReceiveAsync(request.Method, type, id, json, ct);
        return new ReceivedFhirResult(request.Method == "POST" ? 201 : 200, received);
    }
}

/// <summary>Writes FHIR JSON without serializing the JSON body as a quoted string.</summary>
/// <param name="status">Success status of the intake.</param>
/// <param name="received">Stored resource and its exact FHIR response.</param>
internal sealed class ReceivedFhirResult(int status, InboundReceiveResult received) : IResult
{
    /// <inheritdoc/>
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = FhirInboundEndpoints.FhirJson;
        if (status == 201) httpContext.Response.Headers.Location = "/fhir/" + received.ResourceType + "/" + Uri.EscapeDataString(received.ResourceId);
        await httpContext.Response.WriteAsync(received.ResponseBody, httpContext.RequestAborted);
    }
}
