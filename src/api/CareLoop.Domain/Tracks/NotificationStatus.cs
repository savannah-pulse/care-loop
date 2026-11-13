namespace CareLoop.Domain.Tracks;

/// <summary>
/// Status of the notification track.
/// </summary>
/// <remarks>
/// Delivery is not acknowledgement: only <see cref="Acknowledged"/> or <see cref="Understood"/>
/// completes the track.
/// </remarks>
public enum NotificationStatus
{
    /// <summary>At least one channel accepted the message; no delivery report yet.</summary>
    Sent = 0,

    /// <summary>At least one channel reported delivery. The patient has not yet responded.</summary>
    Delivered = 1,

    /// <summary>Every channel failed.</summary>
    Failed = 2,

    /// <summary>The patient acknowledged the message on any channel.</summary>
    Acknowledged = 3,

    /// <summary>The patient confirmed understanding (teach-back).</summary>
    Understood = 4,
}
