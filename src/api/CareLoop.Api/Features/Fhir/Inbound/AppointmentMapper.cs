using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using CareLoop.Domain.Tracks;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>Appointment</c> to an appointment update.
/// </summary>
/// <remarks>
/// Translates <c>booked</c> to scheduled, <c>fulfilled</c> to attended and <c>noshow</c> to missed through
/// <see cref="StatusMap"/>; other statuses produce no event. The appointment names the screening result in
/// <c>supportingInformation</c>, the one field of an <c>Appointment</c> that may point at a <c>DiagnosticReport</c>.
/// <para>
/// When the event happened depends on the status. A booking happened when it was made (<c>created</c>, else
/// <c>meta.lastUpdated</c>), never at the appointment's future <c>start</c>. Attendance and a missed appointment
/// happened at the appointment itself (<c>end</c>, else <c>start</c>, else <c>meta.lastUpdated</c>), never at the
/// booking time, so the timeline stays in order and the time-to-attendance measures are right.
/// </para>
/// </remarks>
public sealed class AppointmentMapper : IFhirInboundMapper
{
    /// <inheritdoc/>
    public string ResourceType => "Appointment";

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        var appointment = (Appointment)resource;

        if (StatusMap.ToAppointmentStatus(appointment.Status) is not { } status)
        {
            IReadOnlyList<EventEnvelope> none = [];
            return System.Threading.Tasks.Task.FromResult(none);
        }

        IReadOnlyList<EventEnvelope> result =
        [
            new EventEnvelope
            {
                SourceSystem = sourceSystem,
                SourceEventId = InboundSupport.SourceEventId(appointment),
                Type = CaseEventType.AppointmentUpdated,
                SourceResultId = InboundSupport.RequireResultId(appointment, appointment.SupportingInformation),
                OccurredAt = status == AppointmentStatus.Scheduled
                    ? InboundSupport.OccurredAt(appointment, InboundSupport.Instant(appointment.CreatedElement))
                    : InboundSupport.OccurredAt(appointment, appointment.End, appointment.Start),
                Actor = appointment.Participant.Select(p => InboundSupport.ReadActor(p.Actor)).FirstOrDefault(a => a is not null),
                Payload = new AppointmentUpdatePayload
                {
                    Status = status,
                    AppointmentReference = InboundSupport.ReferenceTo(appointment),
                    ScheduledFor = appointment.Start,
                },
            },
        ];
        return System.Threading.Tasks.Task.FromResult(result);
    }
}
