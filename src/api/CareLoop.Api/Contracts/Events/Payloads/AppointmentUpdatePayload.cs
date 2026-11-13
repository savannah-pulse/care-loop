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
}
