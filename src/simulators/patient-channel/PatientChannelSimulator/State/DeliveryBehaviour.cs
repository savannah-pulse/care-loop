namespace PatientChannelSimulator.State;

/// <summary>
/// What the channel reports when a notification for a patient arrives.
/// </summary>
/// <remarks>
/// The delivery half of a <see cref="PatientBehaviour"/>. It reproduces the transport outcomes
/// CareLoop must handle. None of these is a patient response: even <see cref="Deliver"/> leaves
/// the notification unacknowledged until the patient's <see cref="PatientResponseBehaviour"/> says otherwise.
/// </remarks>
public enum DeliveryBehaviour
{
    /// <summary>Report the message as delivered (<c>Communication</c> status <c>completed</c>).</summary>
    Deliver,

    /// <summary>Report the delivery as failed (<c>Communication</c> status <c>not-done</c> with a reason).</summary>
    Fail,

    /// <summary>Accept the message but report nothing at all, so CareLoop's deadline handling has to detect the silence.</summary>
    StaySilent,
}
