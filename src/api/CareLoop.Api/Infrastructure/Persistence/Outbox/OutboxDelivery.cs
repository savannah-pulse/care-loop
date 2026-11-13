namespace CareLoop.Api.Infrastructure.Persistence.Outbox;

/// <summary>
/// Tracks the delivery of one outbox message to one subscriber.
/// </summary>
/// <remarks>
/// Delivery is tracked per subscriber so that a failing subscriber is retried on its own and
/// never blocks, or causes a repeat for, the subscribers that already succeeded.
/// </remarks>
public sealed class OutboxDelivery
{
    /// <summary>Identifier of the delivery record.</summary>
    public Guid Id { get; set; }

    /// <summary>The message being delivered.</summary>
    public Guid OutboxMessageId { get; set; }

    /// <summary>The subscriber's stable name.</summary>
    public string Subscriber { get; set; } = string.Empty;

    /// <summary>Whether the delivery is pending, done or parked.</summary>
    public OutboxDeliveryStatus Status { get; set; }

    /// <summary>How many times delivery has been attempted.</summary>
    public int Attempts { get; set; }

    /// <summary>When delivery was last attempted.</summary>
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>When delivery may next be attempted; <see langword="null"/> means immediately.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>When the subscriber handled the event.</summary>
    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>The error from the last failed attempt, without patient detail.</summary>
    public string? LastError { get; set; }
}
