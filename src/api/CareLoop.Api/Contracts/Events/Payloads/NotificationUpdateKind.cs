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
}
