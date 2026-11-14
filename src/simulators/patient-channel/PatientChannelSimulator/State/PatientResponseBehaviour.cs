namespace PatientChannelSimulator.State;

/// <summary>
/// What a patient does, on their own, after a notification has been delivered to them.
/// </summary>
/// <remarks>
/// The response half of a <see cref="PatientBehaviour"/>. A response only ever follows a
/// delivered notification: a patient cannot respond to a message that failed or was never
/// reported. The same responses can also be given by hand from the inbox.
/// </remarks>
public enum PatientResponseBehaviour
{
    /// <summary>The patient does nothing. The notification stays unacknowledged.</summary>
    None,

    /// <summary>The patient confirms they saw the message.</summary>
    Acknowledge,

    /// <summary>The patient confirms they understood the message (teach-back).</summary>
    Understand,

    /// <summary>The patient asks to be called.</summary>
    CallMe,

    /// <summary>
    /// The patient only rates their confidence (C10). A rating is not an acknowledgement, so the
    /// notification stays unacknowledged.
    /// </summary>
    RateConfidence,
}
