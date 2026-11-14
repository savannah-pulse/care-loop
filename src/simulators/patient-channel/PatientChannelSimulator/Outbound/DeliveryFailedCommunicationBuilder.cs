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
/// points back to the originating request through <c>basedOn</c> and to the case <c>Task</c> through
/// <c>about</c>, and has a deterministic id and <c>meta.versionId</c> so CareLoop deduplicates a
/// resend of the same event.
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
    /// <exception cref="ArgumentException"><paramref name="reason"/> is empty: a failure without a reason would give CareLoop nothing to act on.</exception>
    public Communication Build(InboxMessage message, DateTimeOffset occurredAt, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var communication = CommunicationFactory.TransportReport(message, occurredAt, EventStatus.NotDone);
        communication.StatusReason = new CodeableConcept { Text = reason };
        return communication;
    }
}
