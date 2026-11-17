namespace MessagingSimulator.State;

/// <summary>
/// What the simulator did, or deliberately did not do, in reaction to one notification.
/// </summary>
public enum PatientReactionKind
{
    /// <summary>A delivery report was sent: the message reached the recipient.</summary>
    Delivered,

    /// <summary>A failed-delivery report was sent, with its reason.</summary>
    DeliveryFailed,

    /// <summary>Nothing was reported about delivery, on purpose: the channel stayed silent.</summary>
    NoDeliveryReport,

    /// <summary>A reply was sent, as typed. What it means is CareLoop's decision.</summary>
    Replied,

    /// <summary>The patient did not reply to this notification, on purpose.</summary>
    NoResponse,
}
