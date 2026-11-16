using HisSimulator.State;

namespace HisSimulator.Inbound;

/// <summary>Read-only staff inbox; staff notifications arrive as FHIR Tasks, not a second intake route.</summary>
public static class StaffInboxEndpoints
{
    /// <summary>Maps the inbox list and detail reads.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapStaffInboxEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/staff-inbox", (string? recipient, SimulatorStore store, CancellationToken ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return TypedResults.Ok(new StaffInboxResponse(store.GetInboxItems(recipient)));
        }).WithName("ListStaffInbox").WithSummary("Lists simulated staff notices; receipt never acknowledges them.");
        app.MapGet("/staff-inbox/{id:guid}", IResult (Guid id, SimulatorStore store, CancellationToken ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return store.GetInboxItem(id) is { } item ? TypedResults.Ok(item) : TypedResults.NotFound();
        }).WithName("GetStaffInboxItem").Produces<StaffInboxItem>().Produces(404);
        return app;
    }
}
