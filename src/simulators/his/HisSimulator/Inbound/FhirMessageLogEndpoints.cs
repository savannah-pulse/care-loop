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
        app.MapGet("/fhir-log", IResult (string? direction, string? caseRef, string? since, string? limit, SimulatorStore store, CancellationToken ct) =>
        {
            ct.ThrowIfCancellationRequested();
            FhirMessageDirection? filter = direction?.ToLowerInvariant() switch
            {
                null => null, "sent" => FhirMessageDirection.Sent, "received" => FhirMessageDirection.Received,
                _ => throw new ArgumentException("Direction must be sent or received."),
            };
            DateTimeOffset? after = since is null ? null
                : DateTimeOffset.TryParse(since, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var instant) ? instant
                : throw new ArgumentException("Since must be an ISO 8601 instant.");
            int? take = limit is null ? null
                : int.TryParse(limit, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count) && count > 0 ? count
                : throw new ArgumentException("Limit must be a positive integer.");
            return TypedResults.Ok(new FhirMessageLogResponse(store.GetLogEntries(filter, caseRef, after, take)));
        }).WithName("ListFhirMessageLog")
            .WithSummary("Lists synthetic FHIR request and response bodies, newest first; 'since' (ISO 8601 instant) returns only newer entries and 'limit' caps the count.")
            .Produces<FhirMessageLogResponse>().ProducesProblem(400);
        app.MapGet("/fhir-log/{id:guid}", IResult (Guid id, SimulatorStore store, CancellationToken ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return store.GetLogEntry(id) is { } entry ? TypedResults.Ok(entry) : TypedResults.NotFound();
        }).WithName("GetFhirMessageLogEntry").WithSummary("Reads one log entry; the log keeps only the newest entries, so an old id may be gone.")
            .Produces<FhirMessageLogEntry>().Produces(404);
        return app;
    }
}
