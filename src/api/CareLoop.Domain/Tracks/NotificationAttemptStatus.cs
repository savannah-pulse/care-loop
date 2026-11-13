namespace CareLoop.Domain.Tracks;

/// <summary>
/// Outcome of one send on one channel.
/// </summary>
public enum NotificationAttemptStatus
{
    /// <summary>The provider accepted the message.</summary>
    Sent = 0,

    /// <summary>The channel reported delivery.</summary>
    Delivered = 1,

    /// <summary>The provider or channel reported failure.</summary>
    Failed = 2,
}
