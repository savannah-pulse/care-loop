namespace MessagingSimulator.State;

/// <summary>
/// What the simulator has reported to CareLoop about the delivery of one inbox message.
/// </summary>
/// <remarks>
/// Delivery is a transport fact only. A delivered message is not an acknowledgement: it never
/// completes the notification track, transfers ownership or stops a CareLoop deadline. The
/// patient's own replies are kept separately, in <see cref="InboxMessage.Replies"/>.
/// </remarks>
public enum DeliveryState
{
    /// <summary>The request was received and no delivery report has been sent yet.</summary>
    Pending,

    /// <summary>A delivery report with status <c>delivered</c> was sent to CareLoop.</summary>
    Delivered,

    /// <summary>A delivery report with status <c>failed</c> and a reason was sent to CareLoop.</summary>
    Failed,

    /// <summary>
    /// The simulator deliberately reported nothing, so CareLoop's own deadline has to notice the gap.
    /// </summary>
    Silent,
}
