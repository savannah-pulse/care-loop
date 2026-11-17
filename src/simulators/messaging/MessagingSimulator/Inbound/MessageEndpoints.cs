using MessagingSimulator.Outbound;

namespace MessagingSimulator.Inbound;

/// <summary>
/// The route CareLoop's text-message and email providers post to.
/// </summary>
/// <remarks>
/// Plain JSON in and out, the way an SMS or email service is called. No FHIR crosses this route.
/// Errors are answered as problem details.
/// </remarks>
public static class MessageEndpoints
{
    /// <summary>
    /// Maps <c>POST /messages</c>.
    /// </summary>
    /// <param name="routes">The route builder to add the route to.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost(MessageReceiver.Route, async (HttpRequest request, MessageReceiver receiver, CancellationToken ct) =>
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(ct);
                try
                {
                    var received = await receiver.ReceiveAsync(body, ct);
                    var accepted = new MessageAccepted(received.Message.MessageId);
                    return received.Created
                        ? Results.Json(accepted, statusCode: StatusCodes.Status201Created)
                        : Results.Ok(accepted);
                }
                catch (FormatException ex)
                {
                    return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "The message was not accepted.");
                }
                catch (Exception ex) when (ex is HttpRequestException or CareLoopNotConfiguredException)
                {
                    return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "The delivery outcome could not be reported to CareLoop.");
                }
            })
            .WithName("ReceiveMessage")
            .WithSummary("Receives a text message or an email from CareLoop into the simulated inbox (SIMULATED).")
            .WithTags("Messages")
            .Accepts<IncomingMessage>("application/json")
            .Produces<MessageAccepted>(StatusCodes.Status201Created)
            .Produces<MessageAccepted>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return routes;
    }
}
