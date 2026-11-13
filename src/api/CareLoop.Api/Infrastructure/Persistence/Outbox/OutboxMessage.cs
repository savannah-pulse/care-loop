namespace CareLoop.Api.Infrastructure.Persistence.Outbox;

/// <summary>
/// One domain event waiting in the outbox, stored in the same transaction as the case change that raised it.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Identifier of the message; equal to the domain event's identifier so subscribers can deduplicate.</summary>
    public Guid Id { get; set; }

    /// <summary>The case the event is about.</summary>
    public Guid CaseId { get; set; }

    /// <summary>Name of the domain event type, for example <c>EscalationRaised</c>; used to deserialize <see cref="PayloadJson"/>.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The domain event serialized as JSON.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>When the domain event was raised.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>When the message was written to the outbox.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>One delivery record per subscriber.</summary>
    public List<OutboxDelivery> Deliveries { get; set; } = [];
}
