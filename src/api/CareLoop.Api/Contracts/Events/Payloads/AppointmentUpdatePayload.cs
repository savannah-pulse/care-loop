using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;

namespace CareLoop.Contracts.Events.Payloads;

/// <summary>
/// Payload of <see cref="CaseEventType.AppointmentUpdated"/>: the follow-up appointment changed status.
/// </summary>
public sealed record AppointmentUpdatePayload : EventPayload
{
    /// <summary>The new status of the appointment.</summary>
    public required AppointmentStatus Status { get; init; }

    /// <summary>Reference to the source <c>Appointment</c>.</summary>
    public required string AppointmentReference { get; init; }

    /// <summary>When the appointment is or was scheduled for.</summary>
    public DateTimeOffset? ScheduledFor { get; init; }

    /// <summary>
    /// Where the appointment takes place, as the source system names it (for example a clinic name);
    /// <see langword="null"/> when the source did not say.
    /// </summary>
    /// <remarks>Passed to the domain on a booking or rebooking so a consented caregiver can be told the place; it is not stored on the case.</remarks>
    public string? Location { get; init; }
}
