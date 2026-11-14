namespace PatientChannelSimulator.Outbound;

/// <summary>
/// How an attempt to send a patient response ended.
/// </summary>
public enum PatientResponseStatus
{
    /// <summary>CareLoop accepted the response (or had already accepted the same one) and the inbox message shows it.</summary>
    Sent,

    /// <summary>No inbox message has that identifier.</summary>
    NotFound,

    /// <summary>
    /// The message cannot be answered: it is a caregiver's copy, or it was never delivered to the
    /// patient. Nothing was sent.
    /// </summary>
    NotAllowed,

    /// <summary>CareLoop answered but refused the response. The inbox message is unchanged.</summary>
    Rejected,

    /// <summary>CareLoop could not be reached, or the simulator is not configured to sign. The inbox message is unchanged.</summary>
    Unreachable,
}
