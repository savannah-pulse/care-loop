using Hl7.Fhir.Model;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Builds the FHIR <c>Communication</c> sent when the patient asks to be called.
/// </summary>
/// <remarks>
/// Status <c>completed</c>, category <see cref="CommunicationCodes.CallMe"/>, sender the patient.
/// A call-me request is not an acknowledgement: CareLoop raises a next action for the current
/// case owner and keeps its clocks running.
/// Every built resource is tagged as simulated (<see cref="CommunicationCodes.SimulatedTagCode"/>),
/// points back to the originating request through <c>basedOn</c> and to the case <c>Task</c> through
/// <c>about</c>, and has a deterministic id and <c>meta.versionId</c> so CareLoop deduplicates a
/// resend of the same event.
/// </remarks>
public sealed class CallMeCommunicationBuilder
{
    /// <summary>
    /// Builds the resource for one inbox message.
    /// </summary>
    /// <param name="message">The inbox message the event is about; supplies the request, patient and case references.</param>
    /// <param name="occurredAt">When the event happened; becomes <c>sent</c> on the resource.</param>
    /// <returns>A FHIR R4 <c>Communication</c> ready to send to CareLoop.</returns>
    public Communication Build(InboxMessage message, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(message);

        // A patient may ask for a call more than once. Each request is its own event, so it gets
        // the next version; a resend of the same request reuses the version and is deduplicated.
        return CommunicationFactory.PatientResponse(
            message, occurredAt, CommunicationCodes.CallMe, CommunicationFactory.CallMeSuffix, version: message.CallRequestCount + 1);
    }
}
