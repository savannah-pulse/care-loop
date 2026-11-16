namespace PatientChannelSimulator.State;

/// <summary>
/// What a simulated patient does after a notification reaches them.
/// </summary>
/// <remarks>
/// A patient can only reply by text or email. Saying the next step back (teach-back) and rating
/// their confidence are not things a patient does on these channels: a nurse records them after a
/// call, in CareLoop.
/// </remarks>
public enum PatientResponseBehaviour
{
    /// <summary>The patient stays silent. The message stays unacknowledged, which is what lets CareLoop's deadline be demonstrated.</summary>
    None,

    /// <summary>The patient replies <see cref="ReplyCodes.WillCome"/>: "I will come".</summary>
    Acknowledge,

    /// <summary>The patient replies <see cref="ReplyCodes.CallMe"/>: they ask their care team for a call.</summary>
    CallMe,
}

/// <summary>
/// The replies a message invites, as the message text tells the patient.
/// </summary>
/// <remarks>
/// The simulator only uses them to type a reply for a patient whose behaviour says so. What a
/// reply means is decided by CareLoop, which also has to cope with any other text.
/// </remarks>
public static class ReplyCodes
{
    /// <summary>"I will come".</summary>
    public const string WillCome = "1";

    /// <summary>"Please call me".</summary>
    public const string CallMe = "2";
}
