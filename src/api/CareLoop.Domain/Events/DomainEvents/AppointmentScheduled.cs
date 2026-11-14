namespace CareLoop.Domain.Events.DomainEvents;

/// <summary>
/// The follow-up appointment was booked, or booked again at a new time.
/// </summary>
/// <remarks>
/// Raised by <see cref="Cases.Case.ScheduleAppointment"/> on the first booking, on a rebooking after a
/// missed appointment, and when the hospital system moves the appointment to another time. It carries
/// the appointment's date, time and place only, never the result, so a consented caregiver can be told
/// when and where to bring the patient without learning anything clinical. The FHIR boundary publishes
/// nothing for it: the booking came from the hospital system, which already knows. Scheduling is not
/// attendance, so this event never completes the appointment track.
/// </remarks>
public sealed record AppointmentScheduled : IDomainEvent
{
    /// <inheritdoc/>
    public required Guid EventId { get; init; }

    /// <inheritdoc/>
    public required Guid CaseId { get; init; }

    /// <inheritdoc/>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>When the appointment is scheduled for.</summary>
    public required DateTimeOffset ScheduledFor { get; init; }

    /// <summary>
    /// Where the appointment takes place, as the hospital system names it (for example a clinic name);
    /// <see langword="null"/> when the source did not say.
    /// </summary>
    public string? Location { get; init; }
}
