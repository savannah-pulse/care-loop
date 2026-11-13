using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Makes a simulated patient respond to a delivered notification the way their behaviour says.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes the simulator play the patient instead of following a script. After a
/// notification is delivered, the responder reads the patient's <see cref="PatientBehaviour"/>:
/// if this is the notification the patient responds to
/// (<see cref="PatientBehaviour.RespondToNotification"/>), it waits
/// <see cref="PatientBehaviour.RespondAfter"/> and then sends the response through
/// <see cref="PatientResponseSender"/>, exactly as a click in the inbox would.
/// </para>
/// <para>
/// It only ever reacts to a message that was received and delivered. With no notification from
/// CareLoop there is no response, so a scenario cannot appear to pass when CareLoop sent nothing.
/// A patient whose behaviour is <see cref="PatientResponseBehaviour.None"/> stays silent for good.
/// </para>
/// </remarks>
public sealed class AutomaticResponder
{
    private readonly PatientResponseSender _responses;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the responder.
    /// </summary>
    /// <param name="responses">Sends the response to CareLoop and updates the inbox message.</param>
    /// <param name="store">Supplies the patient's behaviour and notification count, and stores the reaction.</param>
    /// <param name="clock">Source of the time the response is due and sent.</param>
    public AutomaticResponder(PatientResponseSender responses, SimulatorStore store, ISimulatorClock clock)
    {
        _responses = responses;
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Decides whether the patient responds to a delivered message and, if so, schedules the response.
    /// </summary>
    /// <param name="message">The inbox message, already marked delivered.</param>
    /// <param name="ct">Cancels the scheduling.</param>
    /// <returns>
    /// A task that completes once the decision is recorded. It does not wait for a delayed
    /// response to be sent; that happens later, at the due time.
    /// </returns>
    public Task ScheduleAsync(InboxMessage message, CancellationToken ct)
    {
        // TODO(WS-8): skip unless the message is Delivered; read SimulatorStore.GetBehaviour and
        // CountNotifications for the patient; if Response is None or this is not the RespondToNotification-th
        // notification, record a NoResponse reaction; otherwise send the matching PatientResponseKind through
        // PatientResponseSender once RespondAfter has passed on the simulator clock, and record the reaction.
        _ = _responses;
        _ = _store;
        _ = _clock;
        throw new NotImplementedException("WS8: AutomaticResponder.ScheduleAsync");
    }
}
