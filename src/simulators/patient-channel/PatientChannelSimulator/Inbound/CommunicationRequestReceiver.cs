using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Handles a FHIR <c>CommunicationRequest</c> posted by CareLoop: a patient notification request.
/// </summary>
/// <remarks>
/// Parses the resource itself with the Firely SDK, logs the exchange, places the message in the
/// simulated patient inbox, hands it to <see cref="DeliverySimulator"/> and then, if it was
/// delivered, to <see cref="AutomaticResponder"/> so the patient can react. Handling is
/// idempotent on the request id because CareLoop's outbox retries deliveries: a resend must not
/// create a second inbox message or a second delivery report.
/// </remarks>
public sealed class CommunicationRequestReceiver
{
    private readonly CommunicationRequestMapper _mapper;
    private readonly DeliverySimulator _delivery;
    private readonly AutomaticResponder _responder;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the receiver.
    /// </summary>
    /// <param name="mapper">Maps the parsed request to an inbox message.</param>
    /// <param name="delivery">Simulates the delivery outcome once the message is stored.</param>
    /// <param name="responder">Lets the patient react to a delivered message according to their behaviour.</param>
    /// <param name="store">Holds the inbox and the FHIR message log.</param>
    /// <param name="clock">Source of the received time.</param>
    public CommunicationRequestReceiver(
        CommunicationRequestMapper mapper,
        DeliverySimulator delivery,
        AutomaticResponder responder,
        SimulatorStore store,
        ISimulatorClock clock)
    {
        _mapper = mapper;
        _delivery = delivery;
        _responder = responder;
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Receives one notification request.
    /// </summary>
    /// <param name="fhirJson">The raw request body: a FHIR R4 <c>CommunicationRequest</c> as JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The inbox message created for the request, or the existing one when the request was already received.</returns>
    /// <exception cref="FormatException">The body is not a valid FHIR <c>CommunicationRequest</c> or cannot be mapped.</exception>
    public Task<InboxMessage> ReceiveAsync(string fhirJson, CancellationToken ct)
    {
        // TODO(WS-8): parse with the Firely JSON parser, append an Inbound FhirMessageLogEntry, return the
        // existing message if the request id was seen before, otherwise map, store, apply delivery and, when delivered, call AutomaticResponder.ScheduleAsync.
        _ = _mapper;
        _ = _delivery;
        _ = _responder;
        _ = _store;
        _ = _clock;
        throw new NotImplementedException("WS8: CommunicationRequestReceiver.ReceiveAsync");
    }
}
