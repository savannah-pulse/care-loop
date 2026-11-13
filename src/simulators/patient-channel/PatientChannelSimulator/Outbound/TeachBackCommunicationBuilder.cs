using Hl7.Fhir.Model;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Builds the FHIR <c>Communication</c> sent when the patient completes teach-back ("I understand").
/// </summary>
/// <remarks>
/// Status <c>completed</c>, category <see cref="CommunicationCodes.TeachBack"/>, sender the patient.
/// Understanding is a stronger statement than acknowledgement and is recorded as its own event;
/// it must only be built from an explicit patient action or a scripted scenario step.
/// Every built resource is tagged as simulated (<see cref="CommunicationCodes.SimulatedTagCode"/>),
/// points back to the originating request through <c>basedOn</c>, and carries a deterministic
/// <c>identifier</c> so CareLoop deduplicates a resend of the same event.
/// </remarks>
public sealed class TeachBackCommunicationBuilder
{
    /// <summary>
    /// Builds the resource for one inbox message.
    /// </summary>
    /// <param name="message">The inbox message the event is about; supplies the request, patient and case references.</param>
    /// <param name="occurredAt">When the event happened; becomes <c>sent</c> on the resource.</param>
    /// <returns>A FHIR R4 <c>Communication</c> ready to send to CareLoop.</returns>
    public Communication Build(InboxMessage message, DateTimeOffset occurredAt)
    {
        // TODO(WS-8): build a Communication from the patient with status completed, category TeachBack, basedOn the CommunicationRequest, the simulated tag and a deterministic identifier.
        throw new NotImplementedException("WS8: TeachBackCommunicationBuilder.Build");
    }
}
