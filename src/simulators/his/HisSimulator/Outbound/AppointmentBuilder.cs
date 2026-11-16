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
        var status = request.Status switch
        {
            "booked" => Appointment.AppointmentStatus.Booked, "fulfilled" => Appointment.AppointmentStatus.Fulfilled,
            "noshow" => Appointment.AppointmentStatus.Noshow,
            _ => throw new ArgumentException("Appointment status must be booked, fulfilled or noshow."),
        };
        var appointment = FhirSupport.Stamp(new Appointment
        {
            Status = status, Start = request.OccurredAt,
            Participant = [new Appointment.ParticipantComponent
            {
                Actor = new ResourceReference("Patient/" + @case.Patient.SyntheticId), Status = ParticipationStatus.Accepted,
            }],
        }, request, FhirSupport.Id(@case.CaseRef + "/appointment"));
        FhirSupport.Result(appointment, @case);
        if (request.Performer is not null) appointment.Participant.Add(new Appointment.ParticipantComponent
        {
            Actor = FhirSupport.Actor(request.Performer), Status = ParticipationStatus.Accepted,
        });
        var scheduledFor = FhirSupport.OptionalText(request, "scheduledFor");
        if (scheduledFor is not null)
        {
            if (!DateTimeOffset.TryParse(scheduledFor, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var time)) throw new ArgumentException("The scheduled time is invalid.");
            appointment.Start = time;
        }
        if (@case.ResourceIds.TryGetValue("referral-service-request", out var referral))
            appointment.BasedOn.Add(new ResourceReference("ServiceRequest/" + referral));
        var location = FhirSupport.OptionalText(request, "location");
        if (location is not null)
        {
            if (!location.StartsWith("SYN", StringComparison.Ordinal)) throw new ArgumentException("The appointment location must be visibly synthetic.");
            appointment.Participant.Add(new Appointment.ParticipantComponent
            {
                Actor = new ResourceReference("Location/" + FhirSupport.Id(location)) { Display = location },
                Status = ParticipationStatus.Accepted,
            });
        }
        return appointment;
    }
}
