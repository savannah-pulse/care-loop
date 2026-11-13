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
/// points back to the originating request through <c>basedOn</c>, and carries a deterministic
/// <c>identifier</c> so CareLoop deduplicates a resend of the same event.
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
        // TODO(WS-8): build a Communication from the patient with status completed, category CallMe, basedOn the CommunicationRequest, the simulated tag and a deterministic identifier.
        throw new NotImplementedException("WS8: CallMeCommunicationBuilder.Build");
    }
}
