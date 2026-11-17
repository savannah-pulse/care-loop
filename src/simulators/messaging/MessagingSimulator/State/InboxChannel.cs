namespace MessagingSimulator.State;

/// <summary>
/// The channel a simulated message arrived on.
/// </summary>
/// <remarks>
/// CareLoop reaches patients, caregivers and staff by text message and email only: there is no
/// health app and no patient portal. It treats channels as system configuration, not preference,
/// and may send the same notification on both at once.
/// </remarks>
public enum InboxChannel
{
    /// <summary>A simulated text message. It has no subject line.</summary>
    Sms,

    /// <summary>A simulated email, with a subject line.</summary>
    Email,
}
