using CareLoop.Api.Features.Outbox;
using CareLoop.Domain.Events.DomainEvents;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Outbox subscriber that sends the direct notifications a domain event calls for.
/// </summary>
/// <remarks>
/// Will resolve the recipients with <see cref="RecipientResolver"/>, choose the template for the
/// event and recipient kind, and send with <see cref="NotificationSender"/>. Staff notifications
/// carry no patient detail: "a case needs your action" plus a link into the HIS. For
/// <see cref="NotificationRequested"/> the resulting attempts are fed back to the case through the
/// event ingestor so the notification track records them. Because deliveries are retried, a
/// notification already sent for an event identifier must not be sent again.
/// </remarks>
public sealed class NotificationRouter : IDomainEventSubscriber
{
    private readonly RecipientResolver _recipients;
    private readonly NotificationSender _sender;

    /// <summary>
    /// Creates the router.
    /// </summary>
    /// <param name="recipients">Resolves who to notify.</param>
    /// <param name="sender">Sends on every enabled, reachable channel.</param>
    public NotificationRouter(RecipientResolver recipients, NotificationSender sender)
    {
        _recipients = recipients;
        _sender = sender;
    }

    /// <inheritdoc/>
    public string Name => "NotificationRouter";

    /// <inheritdoc/>
    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        // TODO(WS-4): resolve recipients, pick the template key per event type, send, record attempts idempotently.
        throw new NotImplementedException("WS4: NotificationRouter.HandleAsync");
    }
}
