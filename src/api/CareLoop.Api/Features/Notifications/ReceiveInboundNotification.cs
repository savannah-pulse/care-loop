using CareLoop.Api.Features.Events;
using CareLoop.Contracts.Events;
using CareLoop.Domain.Recipients;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Slice for <c>POST /notifications/inbound/{provider}</c>: where a notification provider calls CareLoop back.
/// </summary>
/// <remarks>
/// Finds the <see cref="IInboundNotificationHandler"/> of the named provider, but only when that
/// provider is switched on for some message (<see cref="NotificationOptions.IsUsed"/>: enabled, or named in a
/// per-event override), passes it the raw request,
/// and hands every resulting envelope to <c>EventIngestor</c>. The slice holds no business logic and
/// never changes a case itself. This is the only way a delivery report or a patient reply enters
/// CareLoop; it is not a general event endpoint, because the handler accepts only its own provider's format.
/// </remarks>
public static class ReceiveInboundNotification
{
    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapReceiveInboundNotification(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/notifications/inbound/{provider}", Handle)
            .WithName("ReceiveInboundNotification")
            .WithTags("Notifications")
            .Accepts<object>("application/json", "application/fhir+json", "application/x-www-form-urlencoded")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        return routes;
    }

    /// <summary>
    /// Receives one message from a notification provider.
    /// </summary>
    /// <param name="provider">Name of the provider calling back, as used in <see cref="NotificationOptions.EnabledProviders"/> (case-insensitive).</param>
    /// <param name="request">The raw request; its body and headers are passed to the provider's handler untouched.</param>
    /// <param name="handlers">The registered inbound handlers, one per provider that has one.</param>
    /// <param name="options">Which providers are enabled.</param>
    /// <param name="ingestor">The single intake pipeline.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// 200 when every event was applied or was a duplicate; 400 when the message is not in the
    /// provider's format; 401 when it cannot be verified as coming from the provider; 404 when the
    /// provider is unknown, not enabled or has no inbound handler; 422 when a rule refused an event.
    /// </returns>
    public static async Task<IResult> Handle(
        string provider,
        HttpRequest request,
        IEnumerable<IInboundNotificationHandler> handlers,
        IOptions<NotificationOptions> options,
        EventIngestor ingestor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var enabled = Enum.TryParse<NotificationChannel>(provider, ignoreCase: true, out var channel)
            && Enum.IsDefined(channel)
            && !int.TryParse(provider, out _)
            && options.Value.IsUsed(channel);
        var handler = enabled ? handlers.FirstOrDefault(h => h.Channel == channel) : null;
        if (handler is null)
        {
            return Results.NotFound();
        }

        using var reader = new StreamReader(request.Body);
        var message = new InboundNotificationMessage
        {
            ContentType = request.ContentType ?? string.Empty,
            Body = await reader.ReadToEndAsync(ct),
            Headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase),
        };

        IReadOnlyList<EventEnvelope> envelopes;
        try
        {
            envelopes = await handler.TranslateAsync(message, ct);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (FormatException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "The message is not in the provider's format.");
        }

        foreach (var envelope in envelopes)
        {
            var result = await ingestor.IngestAsync(envelope, ct);
            switch (result.Outcome)
            {
                case IngestOutcome.Invalid:
                    return Results.Problem(result.Reason, statusCode: StatusCodes.Status400BadRequest, title: "The event is not valid.");
                case IngestOutcome.Rejected:
                    return Results.Problem(result.Reason, statusCode: StatusCodes.Status422UnprocessableEntity, title: "A rule refused the event.");
            }
        }

        return Results.Ok();
    }
}
