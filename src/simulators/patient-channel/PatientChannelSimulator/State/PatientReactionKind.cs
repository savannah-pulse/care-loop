namespace PatientChannelSimulator.State;

/// <summary>
/// One thing the simulator did in reaction to a notification.
/// </summary>
public enum PatientReactionKind
{
    /// <summary>A delivered report was sent to CareLoop.</summary>
    Delivered,

    /// <summary>A delivery-failed report was sent to CareLoop.</summary>
    DeliveryFailed,

    /// <summary>The message was accepted and, by design, nothing was reported.</summary>
    NoDeliveryReport,

    /// <summary>The patient's acknowledgement was sent to CareLoop.</summary>
    Acknowledged,

    /// <summary>The patient's teach-back confirmation was sent to CareLoop.</summary>
    Understood,

    /// <summary>The patient's request for a call was sent to CareLoop.</summary>
    CallRequested,

    /// <summary>The patient's behaviour is to stay silent, so no response was sent.</summary>
    NoResponse,

    /// <summary>The patient's 1 to 5 confidence rating was sent to CareLoop (C10). A rating is not an acknowledgement.</summary>
    ConfidenceRated,
}
