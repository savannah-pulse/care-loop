using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// The body of <c>POST /api/inbox/{messageId}/reply</c>.
/// </summary>
/// <param name="Text">The reply as the simulated patient typed it.</param>
public sealed record ReplyRequest(string? Text);

/// <summary>
/// Routes the ClientApp uses to show the simulated inboxes and to reply to a message as the patient.
/// </summary>
/// <remarks>
/// The reply route is the only place a patient reply originates outside a patient's behaviour. It
/// is an explicit patient action; nothing here is triggered by delivery. A text message and an
/// email are replied to in the same way.
/// </remarks>
public static class InboxEndpoints
{
    /// <summary>
    /// Maps <c>GET /api/inbox</c>, <c>GET /api/inbox/{messageId}</c> and <c>POST /api/inbox/{messageId}/reply</c>.
    /// </summary>
    /// <param name="routes">The route builder to add the routes to.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapInboxEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/inbox").WithTags("Inbox");

        group.MapGet("/", (SimulatorStore store) => TypedResults.Ok(store.GetInboxMessages()))
            .WithName("ListInboxMessages")
            .WithSummary("Lists every simulated text message and email, newest first (SIMULATED).")
            .Produces<IReadOnlyList<InboxMessage>>();

        group.MapGet("/{messageId}", (string messageId, SimulatorStore store) =>
                store.FindInboxMessage(messageId) is { } message ? Results.Ok(message) : Results.NotFound())
            .WithName("GetInboxMessage")
            .WithSummary("Reads one simulated message (SIMULATED).")
            .Produces<InboxMessage>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{messageId}/reply", async (string messageId, ReplyRequest request, ReplySender sender, CancellationToken ct) =>
                string.IsNullOrWhiteSpace(request.Text)
                    ? Results.Problem(
                        "Type a reply before sending it.",
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "The reply is empty.")
                    : ToResult(await sender.ReplyAsync(messageId, request.Text, ct)))
            .WithName("ReplyToInboxMessage")
            .WithSummary("The patient replies to a text message or an email. The text is sent to CareLoop as typed; CareLoop decides what it means (SIMULATED).")
            .Produces<InboxMessage>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return routes;
    }

    /// <summary>
    /// Turns the outcome of a reply into the HTTP answer of the reply route.
    /// </summary>
    /// <param name="result">The outcome reported by <see cref="ReplySender"/>.</param>
    /// <returns>
    /// 200 with the updated message; 404 for an unknown message; 409 when the message cannot be
    /// replied to; 502 when CareLoop refused the reply or could not be reached.
    /// </returns>
    public static IResult ToResult(ReplyResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Status switch
        {
            ReplyStatus.Sent => Results.Ok(result.Message),
            ReplyStatus.NotFound => Results.NotFound(),
            ReplyStatus.NotAllowed => Results.Problem(result.Error, statusCode: StatusCodes.Status409Conflict, title: "This message cannot be replied to."),
            ReplyStatus.Rejected => Results.Problem(result.Error, statusCode: StatusCodes.Status502BadGateway, title: "CareLoop refused the reply."),
            _ => Results.Problem(result.Error, statusCode: StatusCodes.Status502BadGateway, title: "CareLoop could not be reached."),
        };
    }
}
