namespace CareLoop.Domain.Tracks;

/// <summary>
/// Status of the appointment track, taken from the FHIR <c>Appointment</c>.
/// </summary>
public enum AppointmentStatus
{
    /// <summary>The appointment is booked.</summary>
    Scheduled = 0,

    /// <summary>The patient attended (FHIR <c>fulfilled</c>).</summary>
    Attended = 1,

    /// <summary>The patient did not attend (FHIR <c>noshow</c>); the owner must rebook or follow up.</summary>
    Missed = 2,
}
