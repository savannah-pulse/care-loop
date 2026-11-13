namespace CareLoop.Domain.Events.DomainEvents;

/// <summary>
/// Something CareLoop decided, as a result of a case change, that the outside world must be told about.
/// </summary>
/// <remarks>
/// <para>
/// <b>Definition.</b> A domain event is an outbound decision: a notification is needed, a handoff
/// was proposed, a deadline is approaching, an escalation was raised, a case was closed. It is the
/// opposite direction to a <see cref="CareLoop.Domain.Events.SourceEvent"/>, which records
/// something CareLoop was told.
/// </para>
/// <para>
/// <b>Rules.</b> Domain rules raise domain events on the case; the Domain never sends anything
/// itself. The repository stores them in the outbox in the same transaction as the case change, so
/// an outbound message can never be lost or sent for a change that was rolled back. Subscribers
/// may receive an event more than once and must be idempotent.
/// </para>
/// </remarks>
public interface IDomainEvent
{
    /// <summary>Unique identifier of this occurrence; subscribers use it to deduplicate.</summary>
    Guid EventId { get; }

    /// <summary>The case the event is about.</summary>
    Guid CaseId { get; }

    /// <summary>When the event was raised, taken from <see cref="Time.IClock"/>.</summary>
    DateTimeOffset OccurredAt { get; }
}
