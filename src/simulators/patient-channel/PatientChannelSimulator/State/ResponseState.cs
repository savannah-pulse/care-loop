namespace PatientChannelSimulator.State;

/// <summary>
/// The latest response the simulated patient has given to one inbox message.
/// </summary>
/// <remarks>
/// These are the only patient actions that count as engagement in CareLoop. They are separate
/// from <see cref="DeliveryState"/> because a message can be delivered and never answered,
/// which is exactly the silent-patient case CareLoop must escalate. A confidence rating (C10) is
/// deliberately not a member: it is a measure, not an acknowledgement, and is kept on
/// <see cref="InboxMessage.Confidence"/> so it can never replace <see cref="Acknowledged"/>.
/// </remarks>
public enum ResponseState
{
    /// <summary>The patient has not responded.</summary>
    None,

    /// <summary>The patient confirmed they received the message.</summary>
    Acknowledged,

    /// <summary>The patient completed the teach-back step ("I understand").</summary>
    Understood,

    /// <summary>The patient asked to be called; this creates a next action for the current case owner.</summary>
    CallRequested,
}
