using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Routes the ClientApp uses to show the simulated patient inbox and act on a message.
/// </summary>
/// <remarks>
/// The three action routes are the only places a patient response originates outside a scenario
/// run. Each is an explicit patient action; nothing here is triggered by delivery.
/// </remarks>
public static class InboxEndpoints
{
    /// <summary>
    /// Maps <c>GET /api/inbox</c>, <c>GET /api/inbox/{messageId}</c> and the
    /// <c>acknowledge</c>, <c>understand</c> and <c>call-me</c> actions.
    /// </summary>
    /// <param name="routes">The route builder to add the routes to.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapInboxEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/inbox").WithTags("Inbox");

        group.MapGet("/", () =>
            {
                // TODO(WS-8): return SimulatorStore.GetInboxMessages().
                return NotImplementedResponse.For("GET /api/inbox");
            })
            .WithName("ListInboxMessages")
            .WithSummary("Lists the simulated patient inbox, newest first (SIMULATED).")
            .Produces<IReadOnlyList<InboxMessage>>()
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        group.MapGet("/{messageId}", (string messageId) =>
            {
                // TODO(WS-8): return SimulatorStore.FindInboxMessage(messageId) or 404.
                return NotImplementedResponse.For("GET /api/inbox/{messageId}");
            })
            .WithName("GetInboxMessage")
            .WithSummary("Reads one simulated inbox message (SIMULATED).")
            .Produces<InboxMessage>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        MapAction(group, "acknowledge", "AcknowledgeInboxMessage",
            "The patient acknowledges the message; sends an acknowledgement Communication to CareLoop (SIMULATED).");
        MapAction(group, "understand", "UnderstandInboxMessage",
            "The patient completes teach-back (\"I understand\"); sends a teach-back Communication to CareLoop (SIMULATED).");
        MapAction(group, "call-me", "RequestCallForInboxMessage",
            "The patient asks to be called; sends a call-me Communication to CareLoop (SIMULATED).");

        return routes;
    }

    private static void MapAction(RouteGroupBuilder group, string action, string name, string summary)
    {
        var route = $"POST /api/inbox/{{messageId}}/{action}";
        group.MapPost($"/{{messageId}}/{action}", (string messageId) =>
            {
                // TODO(WS-8): call PatientResponseSender.RespondAsync with the PatientResponseKind for this
                // action; 200 with the updated message, 404 for an unknown message, 502 when CareLoop
                // cannot be reached.
                return NotImplementedResponse.For(route);
            })
            .WithName(name)
            .WithSummary(summary)
            .Produces<InboxMessage>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status502BadGateway)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);
    }
}
