using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Rules;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Applies an appointment status change.
/// </summary>
/// <remarks>
/// Starts or advances the appointment track. Attendance alone does not complete the track; the
/// outcome must also be recorded. A missed appointment gives the current owner a new next action
/// (rebooking) and deadline, and a rebooked appointment gives them the follow-up action
/// (<see cref="HandlerSupport.ArrangeFollowUpAction"/>) so the case is never without a deadline.
/// A booking or rebooking goes through <see cref="Case.ScheduleAppointment"/> with the payload's
/// place, which raises <see cref="Domain.Events.DomainEvents.AppointmentScheduled"/> when the time is new.
/// </remarks>
public sealed class AppointmentUpdateHandler : IEventHandler
{
    private readonly IPathwayProvider _pathways;
    private readonly IClock _clock;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="pathways">Supplies the pathway definition the rules are evaluated against.</param>
    /// <param name="clock">Supplies the current time for deadlines.</param>
    public AppointmentUpdateHandler(IPathwayProvider pathways, IClock clock)
    {
        _pathways = pathways;
        _clock = clock;
    }

    /// <inheritdoc/>
    public CaseEventType EventType => CaseEventType.AppointmentUpdated;

    /// <inheritdoc/>
    /// <remarks>Expects the envelope's payload to be an <see cref="AppointmentUpdatePayload"/>.</remarks>
    public Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct)
    {
        var payload = HandlerSupport.Payload<AppointmentUpdatePayload>(envelope);
        var @case = HandlerSupport.RequireCase(existing, EventType);
        var pathway = _pathways.Get(@case.PathwayId);

        var transition = TransitionRules.Evaluate(@case, EventType, envelope.Actor, pathway);
        if (!transition.Verdict.Allowed)
        {
            return HandlerSupport.Refused(@case, transition.Verdict.Reason!);
        }

        var scheduledFor = payload.ScheduledFor ?? envelope.OccurredAt;

        switch (payload.Status)
        {
            case AppointmentStatus.Scheduled:
                if (@case.Appointment is { } booked)
                {
                    var wasMissed = booked.Status == AppointmentStatus.Missed;
                    if (wasMissed && !HandlerSupport.HasDeadline(@case, HandlerSupport.ArrangeFollowUpAction, pathway, out var missing))
                    {
                        return HandlerSupport.Refused(@case, missing!);
                    }

                    @case.ScheduleAppointment(payload.AppointmentReference, scheduledFor, envelope.OccurredAt, payload.Location);
                    if (wasMissed)
                    {
                        HandlerSupport.SetNextAction(@case, HandlerSupport.ArrangeFollowUpAction, pathway, _clock);
                    }
                }
                else
                {
                    @case.ScheduleAppointment(payload.AppointmentReference, scheduledFor, envelope.OccurredAt, payload.Location);
                }

                break;

            case AppointmentStatus.Attended:
                var attended = @case.Appointment ?? @case.BeginAppointment(payload.AppointmentReference, scheduledFor, envelope.OccurredAt);
                attended.Advance(AppointmentStatus.Attended, envelope.OccurredAt, payload.AppointmentReference);
                break;

            case AppointmentStatus.Missed:
                var rebookAction = HandlerSupport.ExceptionAction(pathway, TrackKind.Appointment);
                if (!HandlerSupport.HasDeadline(@case, rebookAction, pathway, out var noDeadline))
                {
                    return HandlerSupport.Refused(@case, noDeadline!);
                }

                var missed = @case.Appointment ?? @case.BeginAppointment(payload.AppointmentReference, scheduledFor, envelope.OccurredAt);
                missed.Advance(AppointmentStatus.Missed, envelope.OccurredAt, payload.AppointmentReference);
                HandlerSupport.SetNextAction(@case, rebookAction, pathway, _clock);
                break;

            default:
                return HandlerSupport.Refused(@case, $"Appointment status {payload.Status} is not understood.");
        }

        return HandlerSupport.Applied(@case, payload.AppointmentReference);
    }
}
