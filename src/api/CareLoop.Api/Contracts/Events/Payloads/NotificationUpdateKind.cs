namespace CareLoop.Contracts.Events.Payloads;

/// <summary>
/// What a <c>Communication</c> about a notification reports.
/// </summary>
public enum NotificationUpdateKind
{
    /// <summary>A channel reported that the message was delivered.</summary>
    Delivered = 0,

    /// <summary>A channel reported that delivery failed.</summary>
    Failed = 1,

    /// <summary>The patient acknowledged the message.</summary>
    Acknowledged = 2,

    /// <summary>The patient confirmed understanding (teach-back).</summary>
    Understood = 3,

    /// <summary>The patient asked to be called. Not an acknowledgement.</summary>
    CallRequested = 4,

    /// <summary>The patient rated how confident they feel about what to do next, 1 to 5. A measure, not an acknowledgement; the value is in <see cref="NotificationUpdatePayload.Confidence"/>.</summary>
    ConfidenceRated = 5,

    /// <summary>
    /// CareLoop is sending the message on a channel. Recorded before the send, for every channel that can reach the
    /// patient, so a delivery report from one channel is judged against every channel still on its way.
    /// </summary>
    Sent = 6,
}
