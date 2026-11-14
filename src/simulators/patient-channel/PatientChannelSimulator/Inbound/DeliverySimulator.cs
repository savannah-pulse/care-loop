using Hl7.Fhir.Model;
using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Applies the receiving patient's <see cref="DeliveryBehaviour"/> to a newly received inbox message.
/// </summary>
/// <remarks>
/// Looks up the behaviour in force for whoever the message is for, then reports delivered,
/// reports failed, or deliberately reports nothing. It never produces a patient response
/// itself: a delivered message stays unacknowledged until the patient acts, by hand or through
/// <see cref="AutomaticResponder"/>, which is what lets CareLoop's acknowledgement deadline be
/// demonstrated. Every outcome, including a report CareLoop refused, is recorded as a reaction.
/// </remarks>
public sealed class DeliverySimulator
{
    /// <summary>Reason reported when a behaviour says delivery fails but gives no reason.</summary>
    public const string DefaultFailureReason = "SIMULATED: the message could not be delivered.";

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
        var behaviour = _store.GetBehaviour(message.PatientSyntheticId);
        var now = _clock.UtcNow;

        if (behaviour.Delivery == DeliveryBehaviour.StaySilent)
        {
            Record(message, PatientReactionKind.NoDeliveryReport, null, now, null, true, null);
            return _store.UpdateInboxMessage(message.MessageId, m => m with { Delivery = DeliveryState.Silent });
        }

        var fails = behaviour.Delivery == DeliveryBehaviour.Fail;
        var reason = string.IsNullOrWhiteSpace(behaviour.FailureReason) ? DefaultFailureReason : behaviour.FailureReason;
        Communication report = fails ? _failed.Build(message, now, reason) : _delivered.Build(message, now);
        var reaction = fails ? PatientReactionKind.DeliveryFailed : PatientReactionKind.Delivered;
        var sourceEventId = CommunicationFactory.SourceEventId(report);

        FhirSendResult send;
        try
        {
            send = await _careLoop.SendCommunicationAsync(report, ct);
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

            // A server error that outlasted the retries is CareLoop being unavailable, not a decision:
            // failing here lets CareLoop's outbox send the request again, which retries the report.
            // A refusal (4xx) is a decision, and sending again would not change it.
            if (send.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                throw new HttpRequestException(error, null, (System.Net.HttpStatusCode)send.StatusCode);
            }

            return message;
        }

        Record(message, reaction, sourceEventId, now, send.StatusCode, true, null);
        return _store.UpdateInboxMessage(message.MessageId, m => fails
            ? m with { Delivery = DeliveryState.Failed, DeliveryFailureReason = reason }
            : m with { Delivery = DeliveryState.Delivered });
    }

    private void Record(InboxMessage message, PatientReactionKind reaction, string? sourceEventId, DateTimeOffset at, int? statusCode, bool succeeded, string? error) =>
        _store.AppendReaction(new PatientReactionRecord(message.MessageId, message.PatientSyntheticId, reaction, true, sourceEventId, at, statusCode, succeeded, error));
}
