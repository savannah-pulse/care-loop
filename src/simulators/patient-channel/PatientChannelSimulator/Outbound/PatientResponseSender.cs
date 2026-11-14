using Hl7.Fhir.Model;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Turns a patient action on an inbox message into a FHIR <c>Communication</c> and sends it to CareLoop.
/// </summary>
/// <remarks>
/// Used by the inbox action routes and by the automatic responder. The inbox message is updated
/// only after CareLoop accepted the resource, so the simulated inbox never shows a response
/// that CareLoop does not know about. Every attempt, accepted or not, is recorded as a reaction.
/// </remarks>
public sealed class PatientResponseSender
{
    private readonly AcknowledgementCommunicationBuilder _acknowledgement;
    private readonly TeachBackCommunicationBuilder _teachBack;
    private readonly CallMeCommunicationBuilder _callMe;
    private readonly ConfidenceRatingCommunicationBuilder _confidenceRating;
    private readonly CareLoopFhirClient _careLoop;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the sender.
    /// </summary>
    /// <param name="acknowledgement">Builds acknowledgement resources.</param>
    /// <param name="teachBack">Builds teach-back resources.</param>
    /// <param name="callMe">Builds call-me resources.</param>
    /// <param name="confidenceRating">Builds confidence-rating resources.</param>
    /// <param name="careLoop">Sends the built resource to CareLoop.</param>
    /// <param name="store">Holds the inbox message to read and update, and the reactions.</param>
    /// <param name="clock">Source of the response time.</param>
    public PatientResponseSender(
        AcknowledgementCommunicationBuilder acknowledgement,
        TeachBackCommunicationBuilder teachBack,
        CallMeCommunicationBuilder callMe,
        ConfidenceRatingCommunicationBuilder confidenceRating,
        CareLoopFhirClient careLoop,
        SimulatorStore store,
        ISimulatorClock clock)
    {
        _acknowledgement = acknowledgement;
        _teachBack = teachBack;
        _callMe = callMe;
        _confidenceRating = confidenceRating;
        _careLoop = careLoop;
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Records a patient response to an inbox message and reports it to CareLoop.
    /// </summary>
    /// <remarks>
    /// A response is allowed only on a message for the patient that reached them: delivery was
    /// reported, or the channel stayed silent about it. A caregiver's copy can never be answered.
    /// An acknowledgement or teach-back the inbox already shows is not sent again.
    /// </remarks>
    /// <param name="messageId">Simulator-local identifier of the inbox message being answered.</param>
    /// <param name="kind">Which response the patient gave.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <param name="confidence">The 1 to 5 rating; required when <paramref name="kind"/> is <see cref="PatientResponseKind.ConfidenceRating"/> and ignored otherwise.</param>
    /// <param name="automatic">Whether the response comes from the patient's behaviour rather than from someone acting in the ClientApp.</param>
    /// <returns>How the attempt ended, with the inbox message as it is now.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A confidence rating was asked for without a value from 1 to 5.</exception>
    public async Task<PatientResponseResult> RespondAsync(
        string messageId,
        PatientResponseKind kind,
        CancellationToken ct,
        int? confidence = null,
        bool automatic = false)
    {
        if (kind == PatientResponseKind.ConfidenceRating
            && confidence is not (>= ConfidenceRatingCommunicationBuilder.MinimumRating and <= ConfidenceRatingCommunicationBuilder.MaximumRating))
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "A confidence rating must be a whole number from 1 to 5.");
        }

        var message = _store.FindInboxMessage(messageId);
        if (message is null)
        {
            return new PatientResponseResult(PatientResponseStatus.NotFound, null, null, "No inbox message has that identifier.");
        }

        if (message.RecipientKind != RecipientKind.Patient)
        {
            return new PatientResponseResult(PatientResponseStatus.NotAllowed, message, null, "A caregiver's copy cannot be answered: only the patient's own response counts.");
        }

        if (message.Delivery is not (DeliveryState.Delivered or DeliveryState.Silent))
        {
            return new PatientResponseResult(PatientResponseStatus.NotAllowed, message, null, "The message did not reach the patient, so the patient cannot respond to it.");
        }

        if ((kind == PatientResponseKind.Acknowledgement && message.Response == ResponseState.Acknowledged)
            || (kind == PatientResponseKind.TeachBack && message.Response == ResponseState.Understood))
        {
            return new PatientResponseResult(PatientResponseStatus.Sent, message, null);
        }

        var now = _clock.UtcNow;
        var communication = kind switch
        {
            PatientResponseKind.Acknowledgement => _acknowledgement.Build(message, now),
            PatientResponseKind.TeachBack => _teachBack.Build(message, now),
            PatientResponseKind.CallMe => _callMe.Build(message, now),
            PatientResponseKind.ConfidenceRating => _confidenceRating.Build(message, now, confidence!.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown patient response."),
        };
        var reaction = ReactionOf(kind);
        var sourceEventId = CommunicationFactory.SourceEventId(communication);

        FhirSendResult send;
        try
        {
            send = await _careLoop.SendCommunicationAsync(communication, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or CareLoopNotConfiguredException)
        {
            _store.AppendReaction(new PatientReactionRecord(message.MessageId, message.PatientSyntheticId, reaction, automatic, sourceEventId, now, null, false, ex.Message));
            return new PatientResponseResult(PatientResponseStatus.Unreachable, message, null, ex.Message);
        }

        if (!send.Succeeded)
        {
            var error = $"CareLoop answered {send.StatusCode}.";
            _store.AppendReaction(new PatientReactionRecord(message.MessageId, message.PatientSyntheticId, reaction, automatic, sourceEventId, now, send.StatusCode, false, error));
            return new PatientResponseResult(PatientResponseStatus.Rejected, message, send, error);
        }

        var updated = _store.UpdateInboxMessage(message.MessageId, current => kind switch
        {
            PatientResponseKind.Acknowledgement => current with { Response = ResponseState.Acknowledged, RespondedAt = now },
            PatientResponseKind.TeachBack => current with { Response = ResponseState.Understood, RespondedAt = now },
            PatientResponseKind.CallMe => current with { Response = ResponseState.CallRequested, RespondedAt = now, CallRequestCount = current.CallRequestCount + 1 },

            // A rating is a measure, not an acknowledgement: the response state is left alone.
            _ => current with { Confidence = confidence, ConfidenceRatedAt = now, ConfidenceRatingCount = current.ConfidenceRatingCount + 1 },
        });
        _store.AppendReaction(new PatientReactionRecord(message.MessageId, message.PatientSyntheticId, reaction, automatic, sourceEventId, now, send.StatusCode, true, null));
        return new PatientResponseResult(PatientResponseStatus.Sent, updated, send);
    }

    private static PatientReactionKind ReactionOf(PatientResponseKind kind) => kind switch
    {
        PatientResponseKind.Acknowledgement => PatientReactionKind.Acknowledged,
        PatientResponseKind.TeachBack => PatientReactionKind.Understood,
        PatientResponseKind.CallMe => PatientReactionKind.CallRequested,
        PatientResponseKind.ConfidenceRating => PatientReactionKind.ConfidenceRated,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown patient response."),
    };
}
