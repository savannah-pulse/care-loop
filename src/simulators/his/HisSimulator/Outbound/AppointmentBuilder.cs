using HisSimulator.State;
using Hl7.Fhir.Model;

namespace HisSimulator.Outbound;

/// <summary>
/// Builds the <c>Appointment</c> that reports a follow-up appointment being booked, attended or missed.
/// </summary>
/// <remarks>
/// A missed appointment must not park a case, so <c>noshow</c> is sent as its own state for CareLoop
/// to turn into a next action. Appointment times come from the scenario step offsets, never from
/// an invented clinical timeframe.
/// </remarks>
public sealed class AppointmentBuilder
{
    /// <summary>
    /// Builds the appointment in the state named by <see cref="OutboundEventRequest.Status"/>.
    /// </summary>
    /// <param name="case">The simulated case; supplies the patient participant and the referral the appointment is based on.</param>
    /// <param name="request">The event to represent; status must be <c>booked</c>, <c>fulfilled</c> or <c>noshow</c>.</param>
    /// <returns>
    /// An <c>Appointment</c> with the given status, the synthetic patient as participant, a stable logical id across
    /// the appointment's states and a version that changes per state, and a simulated-data tag.
    /// </returns>
    /// <exception cref="ArgumentException">The status is missing or not one of the supported values.</exception>
    public Appointment Build(SimulatedCase @case, OutboundEventRequest request)
    {
        // TODO(WS-7): build the Appointment for the requested state.
        throw new NotImplementedException("WS7: AppointmentBuilder.Build");
    }
}
