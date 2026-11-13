namespace CareLoop.Domain.Recipients;

/// <summary>
/// A channel over which CareLoop can send a notification.
/// </summary>
/// <remarks>
/// Channels are system configuration, not recipient preference: a notification is sent on every
/// enabled channel that can reach the recipient, because redundancy is intended.
/// </remarks>
public enum NotificationChannel
{
    /// <summary>The simulator channel: patient messages go to the patient-channel simulator, staff messages to the HIS simulator's staff inbox. The only channel enabled in the prototype.</summary>
    Simulator = 0,

    /// <summary>Text message. Not allowed to carry sensitive content, so it always receives the SMS-safe template. Not built yet.</summary>
    Sms = 1,

    /// <summary>Email. Not built yet.</summary>
    Email = 2,
}
