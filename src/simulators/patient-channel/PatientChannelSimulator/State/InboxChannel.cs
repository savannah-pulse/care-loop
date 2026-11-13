namespace PatientChannelSimulator.State;

/// <summary>
/// The simulated patient-facing channel a message was presented on.
/// </summary>
/// <remarks>
/// CareLoop treats channels as system configuration, not patient preference, and may send the
/// same notification on several channels at once. The simulator keeps the channel on each inbox
/// message so the UI can show that redundancy and so SMS-safe content can be recognised.
/// </remarks>
public enum InboxChannel
{
    /// <summary>A simulated in-app message. May carry the standard (non SMS-safe) template.</summary>
    App,

    /// <summary>
    /// A simulated SMS. Only the SMS-safe template is expected here: it carries no sensitive
    /// detail, only a prompt to open a secure channel.
    /// </summary>
    Sms,

    /// <summary>A simulated patient-portal message. May carry the standard template.</summary>
    Portal,
}
