namespace CareLoop.Domain.Recipients;

/// <summary>
/// A channel over which CareLoop can send a notification.
/// </summary>
/// <remarks>
/// Channels are system configuration, not recipient preference: a notification is sent on every
/// enabled channel that can reach the recipient, because redundancy is intended. Patients,
/// caregivers and staff are reached by text message and email only: there is no patient app or
/// portal. The values start at 1 because the earlier simulator-only channel, 0, was removed.
/// </remarks>
public enum NotificationChannel
{
    /// <summary>Text message. Not allowed to carry sensitive content, so it always receives the SMS-safe template.</summary>
    Sms = 1,

    /// <summary>Email. Receives the standard template, with its subject line.</summary>
    Email = 2,
}
