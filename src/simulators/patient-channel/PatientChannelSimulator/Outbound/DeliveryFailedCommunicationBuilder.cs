using Hl7.Fhir.Model;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Builds the FHIR <c>Communication</c> that reports a notification delivery as failed.
/// </summary>
/// <remarks>
/// Status <c>not-done</c> with the reason in <c>statusReason</c>, category <see cref="CommunicationCodes.DeliveryStatus"/>.
/// A failure never parks a case: CareLoop turns it into a next action with an owner and a deadline,
/// so the reason must always be present.
/// Every built resource is tagged as simulated (<see cref="CommunicationCodes.SimulatedTagCode"/>),
/// points back to the originating request through <c>basedOn</c>, and carries a deterministic
/// <c>identifier</c> so CareLoop deduplicates a resend of the same event.
/// </remarks>
public sealed class DeliveryFailedCommunicationBuilder
{
    /// <summary>
    /// Builds the resource for one inbox message.
    /// </summary>
    /// <param name="message">The inbox message the event is about; supplies the request, patient and case references.</param>
    /// <param name="occurredAt">When the event happened; becomes <c>sent</c> on the resource.</param>
    /// <param name="reason">Why delivery failed, as free text chosen in the simulator; never clinical content.</param>
    /// <returns>A FHIR R4 <c>Communication</c> ready to send to CareLoop.</returns>
    public Communication Build(InboxMessage message, DateTimeOffset occurredAt, string reason)
    {
        // TODO(WS-8): build a Communication with status not-done, statusReason text = reason, category DeliveryStatus, basedOn the CommunicationRequest, the simulated tag and a deterministic identifier.
        throw new NotImplementedException("WS8: DeliveryFailedCommunicationBuilder.Build");
    }
}
