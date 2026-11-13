using HisSimulator.State;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HisSimulator.Inbound;

/// <summary>
/// Maps the staff inbox routes: reads for the ClientApp and the direct staff-notification intake.
/// </summary>
public static class StaffInboxEndpoints
{
    /// <summary>
    /// Maps <c>GET /staff-inbox</c>, <c>GET /staff-inbox/{id}</c> and <c>POST /staff-inbox/notifications</c>.
    /// </summary>
    /// <param name="app">The route builder to add the endpoints to.</param>
    /// <returns>The same <paramref name="app"/> instance, for chaining.</returns>
    public static IEndpointRouteBuilder MapStaffInboxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/staff-inbox");

        group.MapGet("/", ListInbox)
            .WithName("ListStaffInbox")
            .Produces<StaffInboxResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapGet("/{id:guid}", GetInboxItem)
            .WithName("GetStaffInboxItem")
            .Produces<StaffInboxItem>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPost("/notifications", ReceiveNotification)
            .WithName("ReceiveStaffNotification")
            .Produces<StaffInboxItem>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        return app;
    }

    /// <summary>
    /// Will list staff inbox items, newest first, optionally for one recipient.
    /// </summary>
    /// <param name="recipient">Optional synthetic staff identifier to filter by (query string <c>recipient</c>).</param>
    /// <returns>501 until implemented; then 200 with a <see cref="StaffInboxResponse"/>.</returns>
    private static ProblemHttpResult ListInbox(string? recipient)
    {
        // TODO(WS-7): return SimulatorStore.GetInboxItems(recipient) wrapped in StaffInboxResponse.
        return StubResults.NotImplemented("StaffInboxEndpoints.ListInbox");
    }

    /// <summary>
    /// Will return one staff inbox item.
    /// </summary>
    /// <param name="id">Identifier of the item from the route.</param>
    /// <returns>501 until implemented; then 200 with the item, or 404 if it is unknown.</returns>
    private static ProblemHttpResult GetInboxItem(Guid id)
    {
        // TODO(WS-7): return SimulatorStore.GetInboxItem(id) or 404.
        return StubResults.NotImplemented("StaffInboxEndpoints.GetInboxItem");
    }

    /// <summary>
    /// Will file a direct staff notification into the inbox as a <see cref="StaffInboxItemKind.StaffNotification"/> item.
    /// </summary>
    /// <param name="request">The notification: recipient, message and link, with no patient detail.</param>
    /// <returns>501 until implemented; then 201 with the created item, or 400 for a missing recipient or message.</returns>
    private static ProblemHttpResult ReceiveNotification(StaffNotificationRequest request)
    {
        // TODO(WS-7): validate, create a StaffInboxItem stamped with ISimulatorClock.UtcNow, store it, return 201.
        // Confirm with the owner of CareLoop's SimulatorNotificationProvider whether staff notifications arrive here or as FHIR.
        return StubResults.NotImplemented("StaffInboxEndpoints.ReceiveNotification");
    }
}
