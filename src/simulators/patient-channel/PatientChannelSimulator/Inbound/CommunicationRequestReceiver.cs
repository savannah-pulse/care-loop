using Hl7.Fhir.Model;
using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Handles a FHIR <c>CommunicationRequest</c> posted by CareLoop: a patient notification request.
/// </summary>
/// <remarks>
/// <para>
/// Parses the resource itself with the Firely SDK, places the message in the simulated patient
/// inbox, hands it to <see cref="DeliverySimulator"/> and then, if it was delivered, to
/// <see cref="AutomaticResponder"/> so the patient can react. Handling is idempotent on the
/// request id: a resend must not create a second inbox message, a second delivery report or a
/// second response. The one thing a resend does is finish a delivery that is still unreported.
/// </para>
/// <para>
/// When the delivery outcome cannot be reported, the message is still accepted, as a real channel
/// would accept it, and <see cref="DeliveryReportRetrier"/> reports later. CareLoop does not send
/// a patient notification again, so refusing the request would leave the message undelivered for good.
/// </para>
/// <para>
/// The exchange is logged once, with the status CareLoop actually received. A request that is
/// refused is logged without its body: it was not accepted as synthetic, so it is not kept.
/// </para>
/// </remarks>
public sealed class CommunicationRequestReceiver
{
    /// <summary>The route CareLoop posts notification requests to; used as the URL of the log entry.</summary>
    public const string Route = "/fhir/CommunicationRequest";

    private readonly CommunicationRequestMapper _mapper;
    private readonly DeliverySimulator _delivery;
    private readonly AutomaticResponder _responder;
    private readonly DeliveryReportRetrier _retrier;
    private readonly SimulatorStore _store;
    private readonly MessageGate _gate;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the receiver.
    /// </summary>
    /// <param name="mapper">Maps the parsed request to an inbox message.</param>
    /// <param name="delivery">Simulates the delivery outcome once the message is stored.</param>
    /// <param name="responder">Lets the patient react to a delivered message according to their behaviour.</param>
    /// <param name="retrier">Reports the delivery outcome later when it cannot be reported now.</param>
    /// <param name="gate">Keeps a resend from reporting delivery while the first attempt is still doing so.</param>
    /// <param name="store">Holds the inbox and the FHIR message log.</param>
    /// <param name="clock">Source of the received time.</param>
    public CommunicationRequestReceiver(
        CommunicationRequestMapper mapper,
        DeliverySimulator delivery,
        AutomaticResponder responder,
        DeliveryReportRetrier retrier,
        SimulatorStore store,
        MessageGate gate,
        ISimulatorClock clock)
    {
        _mapper = mapper;
        _delivery = delivery;
        _responder = responder;
        _retrier = retrier;
        _store = store;
        _gate = gate;
        _clock = clock;
    }

    /// <summary>
    /// Receives one notification request.
    /// </summary>
    /// <param name="fhirJson">The raw request body: a FHIR R4 <c>CommunicationRequest</c> as JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The inbox message for the request, and whether this call created it.</returns>
    /// <exception cref="FormatException">The body is not a valid FHIR <c>CommunicationRequest</c> or cannot be mapped.</exception>
    /// <exception cref="HttpRequestException">The delivery outcome could not be reported to CareLoop and background retries are switched off. The message is in the inbox, still pending; a resend of the request tries again.</exception>
    /// <exception cref="CareLoopNotConfiguredException">No signing secret is configured, so the delivery outcome cannot be reported at all. The message is in the inbox, still pending.</exception>
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
            // Refused, so not kept: the body was never accepted as synthetic data.
            Log(now, null, StatusCodes.Status400BadRequest, null);
            throw;
        }

        var created = _store.TryAddInboxMessage(mapped, out var message);
        var status = StatusCodes.Status502BadGateway;
        try
        {
            // A resend changes nothing, unless the delivery outcome is still unreported. Only one
            // request at a time may find that out: a resend that arrives while the first attempt
            // is still reporting waits, and then sees what that attempt achieved.
            var deliveredNow = false;
            using (await _gate.EnterAsync(message.MessageId, ct))
            {
                message = _store.FindInboxMessage(message.MessageId) ?? message;
                if (message.Delivery == DeliveryState.Pending)
                {
                    try
                    {
                        message = await _delivery.ApplyAsync(message, ct);
                        deliveredNow = message.Delivery == DeliveryState.Delivered;
                    }
                    catch (HttpRequestException) when (_retrier.ScheduleFirst(message.MessageId))
                    {
                        // Accepted and still pending; the report is tried again in the background.
                    }
                }
            }

            // Outside the gate, because responding enters it again. Only the request that
            // reported the delivery schedules the patient's response, so it is scheduled once.
            if (deliveredNow)
            {
                await _responder.ScheduleAsync(message, ct);
                message = _store.FindInboxMessage(message.MessageId) ?? message;
            }

            status = created ? StatusCodes.Status201Created : StatusCodes.Status200OK;
            return new ReceivedCommunicationRequest(message, created);
        }
        finally
        {
            Log(now, message.CommunicationRequestId, status, fhirJson);
        }
    }

    private void Log(DateTimeOffset at, string? resourceId, int statusCode, string? body) =>
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
