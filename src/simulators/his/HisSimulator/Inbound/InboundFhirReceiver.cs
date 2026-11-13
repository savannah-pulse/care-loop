using HisSimulator.State;
using HisSimulator.Time;

namespace HisSimulator.Inbound;

/// <summary>
/// Parses FHIR resources sent by CareLoop and files them into the message log and the staff inbox.
/// </summary>
/// <remarks>
/// <para>Expected resources and what they become:</para>
/// <list type="bullet">
/// <item><description><c>Task</c>: escalation notice, proposed handoff, handoff or referral rejection, or call request
/// (<see cref="StaffInboxItemKind"/> is chosen from the task code); addressed to <c>Task.owner</c>.</description></item>
/// <item><description><c>DocumentReference</c>: a handoff summary accompanying a proposed handoff.</description></item>
/// <item><description><c>CommunicationRequest</c>: a staff notification, if CareLoop sends those as FHIR.</description></item>
/// </list>
/// <para>
/// Receiving a message never accepts a handoff or acknowledges anything: a notification is not an
/// acknowledgement. Acceptance or rejection only happens when a person acts and the simulator sends
/// a new outbound event.
/// </para>
/// </remarks>
public sealed class InboundFhirReceiver
{
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the receiver.
    /// </summary>
    /// <param name="store">Receives the log entry and any staff inbox item.</param>
    /// <param name="clock">Timestamps what is received.</param>
    public InboundFhirReceiver(SimulatorStore store, ISimulatorClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Handles one resource posted or put by CareLoop.
    /// </summary>
    /// <param name="method">HTTP method of the request (<c>POST</c> or <c>PUT</c>), recorded in the log.</param>
    /// <param name="resourceType">Resource type from the route.</param>
    /// <param name="resourceId">Logical id from the route for a <c>PUT</c>; <see langword="null"/> for a <c>POST</c>.</param>
    /// <param name="json">The request body: one FHIR R4 resource as JSON.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The log entry id, the inbox item created (if any) and the body to echo back.</returns>
    /// <exception cref="FormatException">The body is not valid FHIR R4 JSON.</exception>
    /// <exception cref="ArgumentException">
    /// The resource's type does not match <paramref name="resourceType"/>, or its id does not match <paramref name="resourceId"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">The resource type is not one the simulated HIS accepts.</exception>
    public Task<InboundReceiveResult> ReceiveAsync(
        string method,
        string resourceType,
        string? resourceId,
        string json,
        CancellationToken cancellationToken)
    {
        // TODO(WS-7): parse with Firely, validate type/id, append a Received FhirMessageLogEntry (also for
        // rejected messages), map Task / DocumentReference / CommunicationRequest to a StaffInboxItem and store it.
        throw new NotImplementedException("WS7: InboundFhirReceiver.ReceiveAsync");
    }
}
