using Hl7.Fhir.Model;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Builds the FHIR <c>Communication</c> that reports a notification as delivered.
/// </summary>
/// <remarks>
/// Status <c>completed</c>, category <see cref="CommunicationCodes.DeliveryStatus"/>, sender the channel and recipient the patient.
/// Delivery is a transport fact only: it is not an acknowledgement, so this resource must never
/// carry a patient-response category and never stops a CareLoop deadline.
/// Every built resource is tagged as simulated (<see cref="CommunicationCodes.SimulatedTagCode"/>),
/// points back to the originating request through <c>basedOn</c> and to the case <c>Task</c> through
/// <c>about</c>, and has a deterministic id and <c>meta.versionId</c> so CareLoop deduplicates a
/// resend of the same event.
/// </remarks>
public sealed class DeliveredCommunicationBuilder
{
    /// <summary>
    /// Builds the resource for one inbox message.
    /// </summary>
    /// <param name="message">The inbox message the event is about; supplies the request, patient and case references.</param>
    /// <param name="occurredAt">When the event happened; becomes <c>sent</c> on the resource.</param>
    /// <returns>A FHIR R4 <c>Communication</c> ready to send to CareLoop.</returns>
    public Communication Build(InboxMessage message, DateTimeOffset occurredAt)
    {
        return CommunicationFactory.TransportReport(message, occurredAt, EventStatus.Completed);
    }
}
