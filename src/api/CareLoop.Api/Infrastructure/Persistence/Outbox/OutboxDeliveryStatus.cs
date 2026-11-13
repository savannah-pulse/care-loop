namespace CareLoop.Api.Infrastructure.Persistence.Outbox;

/// <summary>
/// Where the delivery of one outbox message to one subscriber stands.
/// </summary>
public enum OutboxDeliveryStatus
{
    /// <summary>Not yet delivered, or failed and waiting for its next attempt.</summary>
    Pending = 0,

    /// <summary>The subscriber handled the event.</summary>
    Delivered = 1,

    /// <summary>The attempt limit was reached; a person must look at it. Never silently dropped.</summary>
    Parked = 2,
}
