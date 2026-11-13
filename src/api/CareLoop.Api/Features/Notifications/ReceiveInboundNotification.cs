using CareLoop.Api.Features.Common;
using CareLoop.Contracts.Common;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Slice for <c>POST /notifications/inbound/{provider}</c>: where a notification provider calls CareLoop back.
/// </summary>
/// <remarks>
/// Will find the <see cref="IInboundNotificationHandler"/> of the named provider, but only when that
/// provider is listed in <see cref="NotificationOptions.EnabledProviders"/>, pass it the raw request,
/// and hand every resulting envelope to <c>EventIngestor</c>. The slice holds no business logic and
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
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);
        return routes;
    }

    /// <summary>
    /// Receives one message from a notification provider.
    /// </summary>
    /// <param name="provider">Name of the provider calling back, as used in <see cref="NotificationOptions.EnabledProviders"/> (case-insensitive).</param>
    /// <param name="request">The raw request; its body and headers are passed to the provider's handler untouched.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// 200 when every event was applied or was a duplicate; 400 when the message is not in the
    /// provider's format; 401 when it cannot be verified as coming from the provider; 404 when the
    /// provider is unknown, not enabled or has no inbound handler; 422 when a rule refused an event.
    /// </returns>
    public static IResult Handle(string provider, HttpRequest request, CancellationToken ct)
    {
        // TODO(WS-4): resolve the enabled provider's IInboundNotificationHandler, read the body into an
        // InboundNotificationMessage, call TranslateAsync and pass each envelope to EventIngestor.IngestAsync.
        return NotImplementedResults.For("WS4", "ReceiveInboundNotification");
    }
}
