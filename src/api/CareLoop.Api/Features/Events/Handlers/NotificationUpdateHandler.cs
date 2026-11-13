using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Rules;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Applies a delivery report or a patient response to the notification track.
/// </summary>
/// <remarks>
/// Records the attempt outcome (delivered if any channel delivers, failed only if all fail, then
/// flags unreachable with a next action), completes the track only on acknowledgement or teach-back,
/// and turns a call-me request into a next action for the current owner by raising
/// <see cref="CallRequested"/>. Delivery never completes the track, transfers ownership or stops a
/// clock. An acknowledgement that completes the pending acknowledgement action gives the owner the
/// follow-up action (<see cref="HandlerSupport.ArrangeFollowUpAction"/>) with its own deadline and
/// resolves any escalation raised while the patient was silent.
/// </remarks>
public sealed class NotificationUpdateHandler : IEventHandler
{
    /// <summary>Key of the next action the owner receives when the patient asks to be called.</summary>
    public const string CallPatientAction = "call-patient";

    private readonly IPathwayProvider _pathways;
    private readonly IClock _clock;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="pathways">Supplies the pathway definition the rules are evaluated against.</param>
    /// <param name="clock">Supplies the current time for deadlines.</param>
    public NotificationUpdateHandler(IPathwayProvider pathways, IClock clock)
    {
        _pathways = pathways;
        _clock = clock;
    }

    /// <inheritdoc/>
    public CaseEventType EventType => CaseEventType.NotificationUpdated;

    /// <inheritdoc/>
    /// <remarks>Expects the envelope's payload to be a <see cref="NotificationUpdatePayload"/>.</remarks>
    public Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct)
    {
        var payload = HandlerSupport.Payload<NotificationUpdatePayload>(envelope);
        var @case = HandlerSupport.RequireCase(existing, EventType);
        var pathway = _pathways.Get(@case.PathwayId);

        var transition = TransitionRules.Evaluate(@case, EventType, envelope.Actor, pathway);
        if (!transition.Verdict.Allowed)
        {
            return HandlerSupport.Refused(@case, transition.Verdict.Reason!);
        }

        var track = @case.Notification;
        if (track is null)
        {
            return HandlerSupport.Refused(@case, "No notification has been requested for this case, so there is nothing to report on.");
        }

        return payload.Kind switch
        {
            NotificationUpdateKind.Delivered => RecordDelivery(@case, track, payload, envelope, pathway, NotificationAttemptStatus.Delivered),
            NotificationUpdateKind.Failed => RecordDelivery(@case, track, payload, envelope, pathway, NotificationAttemptStatus.Failed),
            NotificationUpdateKind.Acknowledged => Acknowledge(@case, track, payload, envelope, pathway, NotificationStatus.Acknowledged),
            NotificationUpdateKind.Understood => Acknowledge(@case, track, payload, envelope, pathway, NotificationStatus.Understood),
            NotificationUpdateKind.CallRequested => RequestCall(@case, track, payload, envelope, pathway),
            _ => HandlerSupport.Refused(@case, $"Notification update kind {payload.Kind} is not understood."),
        };
    }

    private Task<EventHandlingResult> RecordDelivery(
        Case @case,
        NotificationTrack track,
        NotificationUpdatePayload payload,
        EventEnvelope envelope,
        PathwayDefinition pathway,
        NotificationAttemptStatus outcome)
    {
        var unreachableAction = HandlerSupport.ExceptionAction(pathway, TrackKind.Notification);
        if (!HandlerSupport.HasDeadline(@case, unreachableAction, pathway, out var missing))
        {
            return HandlerSupport.Refused(@case, missing!);
        }

        var existingAttempt = payload.AttemptId is { } id ? track.Attempts.FirstOrDefault(a => a.Id == id) : null;
        var attempt = existingAttempt is not null
            ? existingAttempt with { Status = outcome, FailureReason = payload.FailureReason ?? existingAttempt.FailureReason }
            : new NotificationAttempt
            {
                Id = payload.AttemptId ?? Guid.NewGuid(),
                Channel = payload.Channel,
                AttemptedAt = envelope.OccurredAt,
                Status = outcome,
                UsedSmsSafeTemplate = payload.Channel == NotificationChannel.Sms,
                FailureReason = payload.FailureReason,
            };

        track.RecordAttempt(attempt);

        if (track.Status == NotificationStatus.Failed)
        {
            @case.FlagException(CaseExceptions.Unreachable, envelope.OccurredAt);
            HandlerSupport.SetNextAction(@case, unreachableAction, pathway, _clock);
        }
        else if (track.Status == NotificationStatus.Delivered)
        {
            @case.ClearException(CaseExceptions.Unreachable, envelope.OccurredAt);
        }

        return HandlerSupport.Applied(@case, payload.CommunicationReference);
    }

    private Task<EventHandlingResult> Acknowledge(
        Case @case,
        NotificationTrack track,
        NotificationUpdatePayload payload,
        EventEnvelope envelope,
        PathwayDefinition pathway,
        NotificationStatus status)
    {
        var completesPendingAction = IsAcknowledgementAction(@case, pathway);
        if (completesPendingAction && !HandlerSupport.HasDeadline(@case, HandlerSupport.ArrangeFollowUpAction, pathway, out var missing))
        {
            return HandlerSupport.Refused(@case, missing!);
        }

        if (track.IsComplete() && status == NotificationStatus.Acknowledged)
        {
            return HandlerSupport.Applied(@case, payload.CommunicationReference);
        }

        track.Advance(status, envelope.OccurredAt, payload.CommunicationReference);
        @case.ClearException(CaseExceptions.Unacknowledged | CaseExceptions.Unreachable, envelope.OccurredAt);

        if (completesPendingAction)
        {
            @case.ResolveEscalations(envelope.OccurredAt);
            HandlerSupport.SetNextAction(@case, HandlerSupport.ArrangeFollowUpAction, pathway, _clock);
        }

        return HandlerSupport.Applied(@case, payload.CommunicationReference);
    }

    private Task<EventHandlingResult> RequestCall(
        Case @case,
        NotificationTrack track,
        NotificationUpdatePayload payload,
        EventEnvelope envelope,
        PathwayDefinition pathway)
    {
        if (!HandlerSupport.HasDeadline(@case, CallPatientAction, pathway, out var missing))
        {
            return HandlerSupport.Refused(@case, missing!);
        }

        track.RecordCallRequest(envelope.OccurredAt, payload.CommunicationReference);
        HandlerSupport.SetNextAction(@case, CallPatientAction, pathway, _clock);

        @case.Raise(new CallRequested
        {
            EventId = Guid.NewGuid(),
            CaseId = @case.Id,
            OccurredAt = _clock.UtcNow,
            CurrentOwner = @case.Owner,
            DueAt = @case.DueAt!.Value,
        });

        return HandlerSupport.Applied(@case, payload.CommunicationReference);
    }

    /// <summary>
    /// Whether the case's current next action is the one the follow-up stage sets on entry, which is
    /// waiting for the patient's acknowledgement.
    /// </summary>
    private static bool IsAcknowledgementAction(Case @case, PathwayDefinition pathway)
    {
        var followUp = pathway.Stages.FirstOrDefault(s => s.Stage == CaseStage.FollowUpActive);
        return followUp?.NextActionOnEntry is { } action
            && string.Equals(@case.NextAction, action, StringComparison.OrdinalIgnoreCase);
    }
}
