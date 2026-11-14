using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Routes the ClientApp uses to show the simulated patient inbox and act on a message.
/// </summary>
/// <remarks>
/// The action routes are the only places a patient response originates outside a patient's
/// behaviour. Each is an explicit patient action; nothing here is triggered by delivery.
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

        group.MapGet("/", (SimulatorStore store) => TypedResults.Ok(store.GetInboxMessages()))
            .WithName("ListInboxMessages")
            .WithSummary("Lists the simulated patient inbox, newest first (SIMULATED).")
            .Produces<IReadOnlyList<InboxMessage>>();

        group.MapGet("/{messageId}", (string messageId, SimulatorStore store) =>
                store.FindInboxMessage(messageId) is { } message ? Results.Ok(message) : Results.NotFound())
            .WithName("GetInboxMessage")
            .WithSummary("Reads one simulated inbox message (SIMULATED).")
            .Produces<InboxMessage>()
            .Produces(StatusCodes.Status404NotFound);

        MapAction(group, "acknowledge", PatientResponseKind.Acknowledgement, "AcknowledgeInboxMessage",
            "The patient acknowledges the message (\"I will come\"); sends an acknowledgement Communication to CareLoop and nothing else (SIMULATED).");
        MapAction(group, "understand", PatientResponseKind.TeachBack, "UnderstandInboxMessage",
            "The patient completes teach-back (\"I understand\"); sends a teach-back Communication to CareLoop (SIMULATED).");
        MapAction(group, "call-me", PatientResponseKind.CallMe, "RequestCallForInboxMessage",
            "The patient asks to be called; sends a call-me Communication to CareLoop (SIMULATED).");

        return routes;
    }

    /// <summary>
    /// Turns the outcome of a patient response into the HTTP answer of an inbox action route.
    /// </summary>
    /// <param name="result">The outcome reported by <see cref="PatientResponseSender"/>.</param>
    /// <returns>
    /// 200 with the updated message; 404 for an unknown message; 409 when the message cannot be
    /// answered; 502 when CareLoop refused the response or could not be reached.
    /// </returns>
    public static IResult ToResult(PatientResponseResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Status switch
        {
            PatientResponseStatus.Sent => Results.Ok(result.Message),
            PatientResponseStatus.NotFound => Results.NotFound(),
            PatientResponseStatus.NotAllowed => Results.Problem(result.Error, statusCode: StatusCodes.Status409Conflict, title: "This message cannot be answered."),
            PatientResponseStatus.Rejected => Results.Problem(result.Error, statusCode: StatusCodes.Status502BadGateway, title: "CareLoop refused the response."),
            _ => Results.Problem(result.Error, statusCode: StatusCodes.Status502BadGateway, title: "CareLoop could not be reached."),
        };
    }

    private static void MapAction(RouteGroupBuilder group, string action, PatientResponseKind kind, string name, string summary)
    {
        group.MapPost($"/{{messageId}}/{action}", async (string messageId, PatientResponseSender sender, CancellationToken ct) =>
                ToResult(await sender.RespondAsync(messageId, kind, ct)))
            .WithName(name)
            .WithSummary(summary)
            .Produces<InboxMessage>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }
}
