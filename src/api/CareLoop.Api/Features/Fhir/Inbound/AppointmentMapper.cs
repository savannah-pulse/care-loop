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
/// The event happened when the source recorded this state: <c>meta.lastUpdated</c>, which moves with every change,
/// so a rebooking or an attendance is never dated at the original booking. Without it, a booking falls back to
/// <c>created</c>, never the appointment's future <c>start</c>; attendance and a missed appointment fall back to the
/// appointment itself (<c>end</c>, else <c>start</c>), never the booking time.
/// </para>
/// <para>
/// The place is the display name of the participant whose actor is a <c>Location</c> (a <c>Location/...</c>
/// reference, an actor typed <c>Location</c>, or a contained <c>Location</c>, whose <c>name</c> is used when the
/// reference carries no display). Without one the payload carries no place.
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
                    ? InboundSupport.OccurredAt(appointment, appointment.Meta?.LastUpdated, InboundSupport.Instant(appointment.CreatedElement))
                    : InboundSupport.OccurredAt(appointment, appointment.Meta?.LastUpdated, appointment.End, appointment.Start),
                Actor = appointment.Participant.Select(p => InboundSupport.ReadActor(p.Actor)).FirstOrDefault(a => a is not null),
                Payload = new AppointmentUpdatePayload
                {
                    Status = status,
                    AppointmentReference = InboundSupport.ReferenceTo(appointment),
                    ScheduledFor = appointment.Start,
                    Location = ReadLocation(appointment),
                },
            },
        ];
        return System.Threading.Tasks.Task.FromResult(result);
    }

    /// <summary>
    /// Reads where the appointment takes place from its <c>Location</c> participant.
    /// </summary>
    /// <param name="appointment">The appointment to read.</param>
    /// <returns>The place's display name, or <see langword="null"/> when no participant names a location.</returns>
    private static string? ReadLocation(Appointment appointment)
    {
        foreach (var participant in appointment.Participant)
        {
            var actor = participant.Actor;
            if (actor is null)
            {
                continue;
            }

            var contained = actor.Reference is { } reference && reference.StartsWith('#')
                ? appointment.Contained.OfType<Location>().FirstOrDefault(l => l.Id == reference[1..])
                : null;

            var isLocation = contained is not null
                || actor.Reference?.StartsWith("Location/", StringComparison.Ordinal) == true
                || string.Equals(actor.Type, "Location", StringComparison.Ordinal);
            if (!isLocation)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(actor.Display) ? contained?.Name : actor.Display;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }
        }

        return null;
    }
}
