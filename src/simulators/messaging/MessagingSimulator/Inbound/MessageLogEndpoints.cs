using MessagingSimulator.State;

namespace MessagingSimulator.Inbound;

/// <summary>
/// Route the ClientApp uses to show the message log.
/// </summary>
public static class MessageLogEndpoints
{
    /// <summary>
    /// Maps <c>GET /api/message-log</c>.
    /// </summary>
    /// <param name="routes">The route builder to add the route to.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMessageLogEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/message-log", (SimulatorStore store) => TypedResults.Ok(store.GetLogEntries()))
            .WithName("ListMessageLog")
            .WithSummary("Lists every exchange between this simulator and CareLoop, newest first (SIMULATED).")
            .WithTags("Message log")
            .Produces<IReadOnlyList<MessageLogEntry>>();

        return routes;
    }
}
