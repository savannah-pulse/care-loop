using MessagingSimulator.Outbound;
using MessagingSimulator.Scenarios;
using MessagingSimulator.State;

namespace MessagingSimulator.Inbound;

/// <summary>
/// Applies the recipient's <see cref="DeliveryBehaviour"/> to a newly received inbox message.
/// </summary>
/// <remarks>
/// Looks up the behaviour in force for whoever the message is for, then reports delivered,
/// reports failed, or deliberately reports nothing. It never produces a patient reply
/// itself: a delivered message stays unacknowledged until the patient acts, by hand or through
/// <see cref="AutomaticResponder"/>, which is what lets CareLoop's acknowledgement deadline be
/// demonstrated. Every outcome, including a report CareLoop refused, is recorded as a reaction.
/// </remarks>
public sealed class DeliverySimulator
{
    /// <summary>Reason reported when a behaviour says delivery fails but gives no reason.</summary>
    public const string DefaultFailureReason = "SIMULATED: the message could not be delivered.";

    private readonly CareLoopCallbackClient _careLoop;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the delivery simulator.
    /// </summary>
    /// <param name="careLoop">Sends the report to CareLoop.</param>
    /// <param name="store">Supplies the patient's behaviour and stores the updated message and the reaction.</param>
    /// <param name="clock">Source of the report time.</param>
    public DeliverySimulator(CareLoopCallbackClient careLoop, SimulatorStore store, ISimulatorClock clock)
    {
        _careLoop = careLoop;
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Simulates the delivery outcome for one inbox message according to the behaviour in force.
    /// </summary>
    /// <param name="message">The message that was just added to the inbox.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>
    /// The message with its new <see cref="DeliveryState"/> (delivered, failed or silent). When
    /// CareLoop refused the report the message is returned still <see cref="DeliveryState.Pending"/>.
    /// </returns>
    /// <exception cref="HttpRequestException">CareLoop could not be reached, or kept answering with a server error; the message stays <see cref="DeliveryState.Pending"/>.</exception>
    /// <exception cref="CareLoopNotConfiguredException">No signing secret is configured; the message stays <see cref="DeliveryState.Pending"/>.</exception>
    public async Task<InboxMessage> ApplyAsync(InboxMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        var behaviour = BehaviourFor(message);
        var now = _clock.UtcNow;

        if (behaviour.Delivery == DeliveryBehaviour.StaySilent)
        {
            Record(message, PatientReactionKind.NoDeliveryReport, null, now, null, true, null);
            return _store.UpdateInboxMessage(message.MessageId, m => m with { Delivery = DeliveryState.Silent });
        }

        var fails = behaviour.Delivery == DeliveryBehaviour.Fail;
        var reason = string.IsNullOrWhiteSpace(behaviour.FailureReason) ? DefaultFailureReason : behaviour.FailureReason;
        var report = fails ? SimulatorCallback.Failed(message, now, reason) : SimulatorCallback.Delivered(message, now);
        var reaction = fails ? PatientReactionKind.DeliveryFailed : PatientReactionKind.Delivered;
        var sourceEventId = report.SourceEventId();

        CallbackSendResult send;
        try
        {
            send = await _careLoop.SendAsync(message.Channel, report, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or CareLoopNotConfiguredException)
        {
            Record(message, reaction, sourceEventId, now, null, false, ex.Message);
            throw;
        }

        if (!send.Succeeded)
        {
            var error = $"CareLoop answered {send.StatusCode}.";
            Record(message, reaction, sourceEventId, now, send.StatusCode, false, error);

            // Whether CareLoop was unavailable (5xx) or refused the report (4xx), it does not know
            // the outcome, so the request must not be answered as a success: CareLoop would count
            // the message as sent while the inbox can never show it as delivered. Failing lets
            // CareLoop's outbox send again, which reports again once the cause is put right.
            throw new HttpRequestException(error, null, (System.Net.HttpStatusCode)send.StatusCode);
        }

        Record(message, reaction, sourceEventId, now, send.StatusCode, true, null);
        return _store.UpdateInboxMessage(message.MessageId, m => fails
            ? m with { Delivery = DeliveryState.Failed, DeliveryFailureReason = reason }
            : m with { Delivery = DeliveryState.Delivered });
    }

    // The default behaviour describes patients. A caregiver's or a staff member's message follows a
    // behaviour only when one was set for that very address; otherwise it is simply delivered, so
    // testing unreachable patients does not also make every caregiver and nurse unreachable.
    private PatientBehaviour BehaviourFor(InboxMessage message) =>
        message.RecipientKind == RecipientKind.Patient
            ? _store.GetBehaviour(message.To)
            : _store.FindOwnBehaviour(message.To) ?? new PatientBehaviour(DeliveryBehaviour.Deliver, PatientResponseBehaviour.None);

    private void Record(InboxMessage message, PatientReactionKind reaction, string? sourceEventId, DateTimeOffset at, int? statusCode, bool succeeded, string? error) =>
        _store.AppendReaction(new PatientReactionRecord(message.MessageId, message.To, reaction, true, sourceEventId, at, statusCode, succeeded, error));
}
