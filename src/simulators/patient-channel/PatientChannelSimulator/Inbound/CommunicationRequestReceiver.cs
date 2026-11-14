using Hl7.Fhir.Model;
using PatientChannelSimulator.Outbound;
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
/// create a second inbox message, a second delivery report or a second response. The one thing a
/// resend does is finish a delivery that could not be reported the first time.
/// </remarks>
public sealed class CommunicationRequestReceiver
{
    /// <summary>The route CareLoop posts notification requests to; used as the URL of the log entry.</summary>
    public const string Route = "/fhir/CommunicationRequest";

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
    /// <returns>The inbox message for the request, and whether this call created it.</returns>
    /// <exception cref="FormatException">The body is not a valid FHIR <c>CommunicationRequest</c> or cannot be mapped.</exception>
    /// <exception cref="HttpRequestException">The delivery outcome could not be reported to CareLoop. The message is in the inbox, still pending; a resend of the request tries again.</exception>
    /// <exception cref="CareLoopNotConfiguredException">No signing secret is configured, so the delivery outcome could not be reported. The message is in the inbox, still pending.</exception>
    public async Task<ReceivedCommunicationRequest> ReceiveAsync(string fhirJson, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        InboxMessage mapped;
        try
        {
            mapped = _mapper.Map(FhirJson.Parse<CommunicationRequest>(fhirJson), now);
        }
        catch (FormatException)
        {
            Log(now, null, StatusCodes.Status400BadRequest, fhirJson);
            throw;
        }

        var created = _store.TryAddInboxMessage(mapped, out var message);
        Log(now, message.CommunicationRequestId, created ? StatusCodes.Status201Created : StatusCodes.Status200OK, fhirJson);

        // A resend changes nothing, unless the first attempt never managed to report a delivery outcome.
        if (created || message.Delivery == DeliveryState.Pending)
        {
            message = await _delivery.ApplyAsync(message, ct);
            if (message.Delivery == DeliveryState.Delivered)
            {
                await _responder.ScheduleAsync(message, ct);
                message = _store.FindInboxMessage(message.MessageId) ?? message;
            }
        }

        return new ReceivedCommunicationRequest(message, created);
    }

    private void Log(DateTimeOffset at, string? resourceId, int statusCode, string body) =>
        _store.AppendLogEntry(new FhirMessageLogEntry(
            Guid.NewGuid().ToString("N"),
            at,
            FhirMessageDirection.Inbound,
            "POST",
            Route,
            "CommunicationRequest",
            resourceId,
            statusCode,
            body));
}
