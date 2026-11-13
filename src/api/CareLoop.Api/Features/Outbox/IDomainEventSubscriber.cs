using CareLoop.Domain.Events.DomainEvents;

namespace CareLoop.Api.Features.Outbox;

/// <summary>
/// Receives domain events from the outbox and turns them into something outbound.
/// </summary>
/// <remarks>
/// The outbox dispatcher delivers every event to every subscriber and tracks delivery per
/// subscriber, so one failing subscriber neither blocks the others nor loses the event. Because
/// a delivery may be retried, an implementation must be idempotent: handling the same
/// <see cref="IDomainEvent.EventId"/> twice must have the effect of handling it once.
/// </remarks>
public interface IDomainEventSubscriber
{
    /// <summary>Stable name of the subscriber; stored with each delivery record, so it must not change.</summary>
    string Name { get; }

    /// <summary>
    /// Handles one domain event.
    /// </summary>
    /// <param name="domainEvent">The event to handle.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>A task that completes when the event has been handled; completing without an exception marks the delivery as done.</returns>
    /// <remarks>Throwing marks the delivery as failed and schedules a retry for this subscriber only.</remarks>
    Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct);
}
