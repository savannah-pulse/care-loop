using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Applies the receiving patient's <see cref="DeliveryBehaviour"/> to a newly received inbox message.
/// </summary>
/// <remarks>
/// Looks up the behaviour in force for the patient the message is for, then reports delivered,
/// reports failed, or deliberately reports nothing. It never produces a patient response
/// itself: a delivered message stays unacknowledged until the patient acts, by hand or through
/// <see cref="AutomaticResponder"/>, which is what lets CareLoop's acknowledgement deadline be
/// demonstrated.
/// </remarks>
public sealed class DeliverySimulator
{
    private readonly DeliveredCommunicationBuilder _delivered;
    private readonly DeliveryFailedCommunicationBuilder _failed;
    private readonly CareLoopFhirClient _careLoop;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the delivery simulator.
    /// </summary>
    /// <param name="delivered">Builds the delivered report.</param>
    /// <param name="failed">Builds the failed report.</param>
    /// <param name="careLoop">Sends the report to CareLoop.</param>
    /// <param name="store">Supplies the patient's behaviour and stores the updated message and the reaction.</param>
    /// <param name="clock">Source of the report time.</param>
    public DeliverySimulator(
        DeliveredCommunicationBuilder delivered,
        DeliveryFailedCommunicationBuilder failed,
        CareLoopFhirClient careLoop,
        SimulatorStore store,
        ISimulatorClock clock)
    {
        _delivered = delivered;
        _failed = failed;
        _careLoop = careLoop;
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Simulates the delivery outcome for one inbox message according to the current setting.
    /// </summary>
    /// <param name="message">The message that was just added to the inbox.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The message with its new <see cref="DeliveryState"/> (delivered, failed or silent).</returns>
    /// <exception cref="HttpRequestException">CareLoop could not be reached; the message stays <see cref="DeliveryState.Pending"/>.</exception>
    public Task<InboxMessage> ApplyAsync(InboxMessage message, CancellationToken ct)
    {
        // TODO(WS-8): read SimulatorStore.GetBehaviour for the message's patient; Deliver -> send the delivered
        // Communication, Fail -> send the failed Communication with the behaviour's reason, StaySilent -> send
        // nothing; record a PatientReactionRecord, then store and return the message with the matching DeliveryState.
        _ = _delivered;
        _ = _failed;
        _ = _careLoop;
        _ = _store;
        _ = _clock;
        throw new NotImplementedException("WS8: DeliverySimulator.ApplyAsync");
    }
}
