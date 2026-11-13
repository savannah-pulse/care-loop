using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Turns a patient action on an inbox message into a FHIR <c>Communication</c> and sends it to CareLoop.
/// </summary>
/// <remarks>
/// Used by the inbox action routes. The inbox message's <see cref="ResponseState"/> is updated
/// only after CareLoop accepted the resource, so the simulated inbox never shows a response
/// that CareLoop does not know about.
/// </remarks>
public sealed class PatientResponseSender
{
    private readonly AcknowledgementCommunicationBuilder _acknowledgement;
    private readonly TeachBackCommunicationBuilder _teachBack;
    private readonly CallMeCommunicationBuilder _callMe;
    private readonly CareLoopFhirClient _careLoop;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the sender.
    /// </summary>
    /// <param name="acknowledgement">Builds acknowledgement resources.</param>
    /// <param name="teachBack">Builds teach-back resources.</param>
    /// <param name="callMe">Builds call-me resources.</param>
    /// <param name="careLoop">Sends the built resource to CareLoop.</param>
    /// <param name="store">Holds the inbox message to read and update.</param>
    /// <param name="clock">Source of the response time.</param>
    public PatientResponseSender(
        AcknowledgementCommunicationBuilder acknowledgement,
        TeachBackCommunicationBuilder teachBack,
        CallMeCommunicationBuilder callMe,
        CareLoopFhirClient careLoop,
        SimulatorStore store,
        ISimulatorClock clock)
    {
        _acknowledgement = acknowledgement;
        _teachBack = teachBack;
        _callMe = callMe;
        _careLoop = careLoop;
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Records a patient response to an inbox message and reports it to CareLoop.
    /// </summary>
    /// <param name="messageId">Simulator-local identifier of the inbox message being answered.</param>
    /// <param name="kind">Which response the patient gave.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The updated inbox message, or <see langword="null"/> when no message has that identifier.</returns>
    /// <exception cref="HttpRequestException">CareLoop could not be reached; the inbox message is left unchanged.</exception>
    public Task<InboxMessage?> RespondAsync(string messageId, PatientResponseKind kind, CancellationToken ct)
    {
        // TODO(WS-8): find the message, build the Communication with the builder for the kind, send it
        // through CareLoopFhirClient and, on success, store the new ResponseState and RespondedAt.
        _ = _acknowledgement;
        _ = _teachBack;
        _ = _callMe;
        _ = _careLoop;
        _ = _store;
        _ = _clock;
        throw new NotImplementedException("WS8: PatientResponseSender.RespondAsync");
    }
}
