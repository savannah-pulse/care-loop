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
/// <para>
/// Every channel CareLoop sends on is recorded as an attempt before the send
/// (<see cref="NotificationUpdateKind.Sent"/>), so a report from one channel is judged against all of them:
/// the notification is delivered if any channel delivers and failed only when every channel has failed,
/// which flags the patient unreachable with a next action. A delivery after that puts the case back to
/// waiting for the patient's answer. Delivery never completes the track, transfers ownership or stops a clock.
/// </para>
/// <para>
/// The track completes only on acknowledgement or teach-back. An acknowledgement that answers what the case is
/// waiting for (the acknowledgement itself, or trying another way to reach the patient) gives the owner the
/// follow-up action (<see cref="HandlerSupport.ArrangeFollowUpAction"/>) with its own deadline and resolves any
/// escalation raised meanwhile. A call-me request becomes a next action for the current owner by raising
/// <see cref="CallRequested"/>; a text-message "will come" does not answer it, because the patient still asked
/// to be rung.
/// </para>
/// <para>
/// An outcome a nurse records after a call (a person as actor, no channel) answers the call request: the
/// acknowledgement or teach-back moves the case on as above, and a confidence rating on its own puts the case back
/// to waiting for the acknowledgement, so nobody is chased for a call that was made. A rating is otherwise a
/// measure and changes nothing else. Each applied update carries a plain-words summary for the timeline.
/// </para>
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
            NotificationUpdateKind.Sent => RecordSent(@case, track, payload, envelope),
            NotificationUpdateKind.Delivered => RecordDelivery(@case, track, payload, envelope, pathway, NotificationAttemptStatus.Delivered),
            NotificationUpdateKind.Failed => RecordDelivery(@case, track, payload, envelope, pathway, NotificationAttemptStatus.Failed),
            NotificationUpdateKind.Acknowledged => Acknowledge(@case, track, payload, envelope, pathway, NotificationStatus.Acknowledged),
            NotificationUpdateKind.Understood => Acknowledge(@case, track, payload, envelope, pathway, NotificationStatus.Understood),
            NotificationUpdateKind.CallRequested => RequestCall(@case, track, payload, envelope, pathway),
            NotificationUpdateKind.ConfidenceRated => RateConfidence(@case, track, payload, envelope, pathway),
            _ => HandlerSupport.Refused(@case, $"Notification update kind {payload.Kind} is not understood."),
        };
    }

    /// <summary>
    /// Whether the update records what a member of staff heard from the patient on a call, rather than something
    /// that came back on a text message or email: a person is its actor and no channel carried it.
    /// </summary>
    private static bool IsFromACall(EventEnvelope envelope, NotificationUpdatePayload payload) =>
        envelope.Actor is not null && payload.Channel is null;

    private static Task<EventHandlingResult> RecordSent(
        Case @case,
        NotificationTrack track,
        NotificationUpdatePayload payload,
        EventEnvelope envelope)
    {
        if (payload.Channel is not { } channel || payload.AttemptId is not { } id)
        {
            return HandlerSupport.Refused(@case, "A send must name its channel and attempt.");
        }

        var summary = $"Sending by {WireName(channel)}.";
        if (track.Attempts.Any(a => a.Id == id))
        {
            // Already known, perhaps already delivered or failed: a send never moves an attempt back.
            return HandlerSupport.Applied(@case, payload.CommunicationReference, summary);
        }

        track.RecordAttempt(new NotificationAttempt
        {
            Id = id,
            Channel = channel,
            AttemptedAt = envelope.OccurredAt,
            Status = NotificationAttemptStatus.Sent,
            UsedSmsSafeTemplate = channel == NotificationChannel.Sms,
        });
        return HandlerSupport.Applied(@case, payload.CommunicationReference, summary);
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

        if (payload.Channel is not { } channel)
        {
            return HandlerSupport.Refused(@case, "A delivery report must name the channel it came from.");
        }

        var existingAttempt = payload.AttemptId is { } id ? track.Attempts.FirstOrDefault(a => a.Id == id) : null;
        var attempt = existingAttempt is not null
            ? existingAttempt with { Status = outcome, FailureReason = payload.FailureReason ?? existingAttempt.FailureReason }
            : new NotificationAttempt
            {
                Id = payload.AttemptId ?? Guid.NewGuid(),
                Channel = channel,
                AttemptedAt = envelope.OccurredAt,
                Status = outcome,
                UsedSmsSafeTemplate = channel == NotificationChannel.Sms,
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
            if (string.Equals(@case.NextAction, unreachableAction, StringComparison.OrdinalIgnoreCase)
                && EntryAction(pathway) is { } entry
                && HandlerSupport.HasDeadline(@case, entry, pathway, out _))
            {
                // Another channel got through after all, so trying another way is no longer the next step:
                // waiting for the patient's answer is, with its own deadline.
                HandlerSupport.SetNextAction(@case, entry, pathway, _clock);
            }
        }

        var summary = outcome == NotificationAttemptStatus.Delivered
            ? $"Delivered by {WireName(channel)}."
            : $"Not delivered by {WireName(channel)}.";
        return HandlerSupport.Applied(@case, payload.CommunicationReference, summary);
    }

    private Task<EventHandlingResult> Acknowledge(
        Case @case,
        NotificationTrack track,
        NotificationUpdatePayload payload,
        EventEnvelope envelope,
        PathwayDefinition pathway,
        NotificationStatus status)
    {
        var fromCall = IsFromACall(envelope, payload);
        var completesPendingAction = IsWaitingForThePatient(@case, pathway, fromCall);
        if (completesPendingAction && !HandlerSupport.HasDeadline(@case, HandlerSupport.ArrangeFollowUpAction, pathway, out var missing))
        {
            return HandlerSupport.Refused(@case, missing!);
        }

        var summary = Describe(status, fromCall);
        if (fromCall)
        {
            track.RecordCallMade(envelope.OccurredAt, payload.CommunicationReference);
        }

        if (!(track.IsComplete() && status == NotificationStatus.Acknowledged))
        {
            track.Advance(status, envelope.OccurredAt, payload.CommunicationReference);
            @case.ClearException(CaseExceptions.Unacknowledged | CaseExceptions.Unreachable, envelope.OccurredAt);
        }

        if (completesPendingAction)
        {
            @case.ResolveEscalations(envelope.OccurredAt);
            HandlerSupport.SetNextAction(@case, HandlerSupport.ArrangeFollowUpAction, pathway, _clock);
        }

        return HandlerSupport.Applied(@case, payload.CommunicationReference, summary);
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

        return HandlerSupport.Applied(@case, payload.CommunicationReference,
            payload.Channel is { } channel ? $"The patient asked for a call, by {WireName(channel)}." : "The patient asked for a call.");
    }

    private Task<EventHandlingResult> RateConfidence(
        Case @case,
        NotificationTrack track,
        NotificationUpdatePayload payload,
        EventEnvelope envelope,
        PathwayDefinition pathway)
    {
        if (payload.Confidence is not { } confidence)
        {
            return HandlerSupport.Refused(@case, "A confidence rating must carry a value.");
        }

        if (confidence is < NotificationTrack.MinimumConfidence or > NotificationTrack.MaximumConfidence)
        {
            return HandlerSupport.Refused(@case,
                $"A confidence rating must be between {NotificationTrack.MinimumConfidence} and {NotificationTrack.MaximumConfidence}; {confidence} given.");
        }

        var fromCall = IsFromACall(envelope, payload);
        var answersCall = fromCall && string.Equals(@case.NextAction, CallPatientAction, StringComparison.OrdinalIgnoreCase);
        var nextAction = track.IsComplete() ? HandlerSupport.ArrangeFollowUpAction : EntryAction(pathway);
        if (answersCall && (nextAction is null || !HandlerSupport.HasDeadline(@case, nextAction, pathway, out _)))
        {
            return HandlerSupport.Refused(@case, $"The pathway gives no deadline for what follows the call ({nextAction ?? "no entry action"}).");
        }

        track.RateConfidence(confidence, envelope.OccurredAt, payload.CommunicationReference);
        if (fromCall)
        {
            track.RecordCallMade(envelope.OccurredAt, payload.CommunicationReference);
        }

        if (answersCall)
        {
            // The call is made. A rating alone is not an acknowledgement, so unless the patient already
            // acknowledged, the next step is waiting for that again, with a fresh deadline.
            @case.ResolveEscalations(envelope.OccurredAt);
            HandlerSupport.SetNextAction(@case, nextAction!, pathway, _clock);
        }

        return HandlerSupport.Applied(@case, payload.CommunicationReference, fromCall
            ? $"On a call, the patient rated their confidence {confidence} out of 5."
            : $"The patient rated their confidence {confidence} out of 5.");
    }

    private static string Describe(NotificationStatus status, bool fromCall) => (status, fromCall) switch
    {
        (NotificationStatus.Understood, true) => "On a call, the patient said the next step back in their own words.",
        (NotificationStatus.Understood, false) => "The patient confirmed they understood the next step.",
        (_, true) => "On a call, the patient said they will come.",
        _ => "The patient replied that they will come.",
    };

    private static string WireName(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Sms => "text message",
        NotificationChannel.Email => "email",
        _ => channel.ToString(),
    };

    /// <summary>The next action the follow-up stage sets on entry: waiting for the patient's acknowledgement.</summary>
    private static string? EntryAction(PathwayDefinition pathway) =>
        pathway.Stages.FirstOrDefault(s => s.Stage == CaseStage.FollowUpActive)?.NextActionOnEntry;

    /// <summary>
    /// Whether the case's next action is one an acknowledgement answers: waiting for it, trying another way to
    /// reach the patient, or (for an outcome recorded after a call) the call itself. A text-message "will come"
    /// does not answer a call the patient asked for: someone still has to ring them.
    /// </summary>
    private static bool IsWaitingForThePatient(Case @case, PathwayDefinition pathway, bool fromCall)
    {
        var next = @case.NextAction;
        return IsAcknowledgementAction(@case, pathway)
            || string.Equals(next, HandlerSupport.ExceptionAction(pathway, TrackKind.Notification), StringComparison.OrdinalIgnoreCase)
            || (fromCall && string.Equals(next, CallPatientAction, StringComparison.OrdinalIgnoreCase));
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
