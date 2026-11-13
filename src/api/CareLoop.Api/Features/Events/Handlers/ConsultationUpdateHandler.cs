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
/// Applies a consultation status change, including the ownership handoff it carries.
/// </summary>
/// <remarks>
/// Starts or advances the consultation track. On a proposed owner it uses
/// <see cref="OwnershipRules.TryPropose"/>, so the current owner stays accountable; on acceptance
/// it transfers ownership through <see cref="OwnershipRules.CanRespond"/> and
/// <see cref="Case.AcceptOwnership"/>; on rejection it returns the next action to the current owner
/// with a new deadline and flags the exception. When the source names no actor for a response, the
/// proposed owner is taken to be responding, because the consultation task belongs to them.
/// A request gives the case the <see cref="ConsultationTrack.AnswerAction"/> next action with the
/// pathway's answer deadline; the answer (acceptance or recorded outcome) completes that action,
/// resolves any escalation raised while it was overdue and hands the owner the follow-up action.
/// </remarks>
public sealed class ConsultationUpdateHandler : IEventHandler
{
    private readonly IPathwayProvider _pathways;
    private readonly IClock _clock;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="pathways">Supplies the pathway definition the rules are evaluated against.</param>
    /// <param name="clock">Supplies the current time for deadlines.</param>
    public ConsultationUpdateHandler(IPathwayProvider pathways, IClock clock)
    {
        _pathways = pathways;
        _clock = clock;
    }

    /// <inheritdoc/>
    public CaseEventType EventType => CaseEventType.ConsultationUpdated;

    /// <inheritdoc/>
    /// <remarks>Expects the envelope's payload to be a <see cref="ConsultationUpdatePayload"/>.</remarks>
    public Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct)
    {
        var payload = HandlerSupport.Payload<ConsultationUpdatePayload>(envelope);
        var @case = HandlerSupport.RequireCase(existing, EventType);
        var pathway = _pathways.Get(@case.PathwayId);

        var transition = TransitionRules.Evaluate(@case, EventType, envelope.Actor, pathway);
        if (!transition.Verdict.Allowed)
        {
            return HandlerSupport.Refused(@case, transition.Verdict.Reason!);
        }

        return payload.Status switch
        {
            ConsultationStatus.Requested => Request(@case, payload, envelope, pathway),
            ConsultationStatus.OwnerProposed => Propose(@case, payload, envelope, pathway),
            ConsultationStatus.Accepted => Accept(@case, payload, envelope, pathway),
            ConsultationStatus.Rejected => Reject(@case, payload, envelope, pathway),
            ConsultationStatus.OutcomeRecorded => RecordOutcome(@case, payload, envelope, pathway),
            _ => HandlerSupport.Refused(@case, $"Consultation status {payload.Status} is not understood."),
        };
    }

    private Task<EventHandlingResult> Request(Case @case, ConsultationUpdatePayload payload, EventEnvelope envelope, PathwayDefinition pathway)
    {
        if (!HandlerSupport.HasDeadline(@case, ConsultationTrack.AnswerAction, pathway, out var missing))
        {
            return HandlerSupport.Refused(@case, missing!);
        }

        var track = @case.Consultation;
        if (track is null)
        {
            @case.BeginConsultation(payload.ServiceRequestReference, envelope.OccurredAt);
        }
        else
        {
            track.Advance(ConsultationStatus.Requested, envelope.OccurredAt, payload.ServiceRequestReference);
            @case.ClearException(CaseExceptions.Rejected, envelope.OccurredAt);
        }

        HandlerSupport.SetNextAction(@case, ConsultationTrack.AnswerAction, pathway, _clock);

        return HandlerSupport.Applied(@case, payload.TaskReference);
    }

    /// <summary>
    /// When the case was waiting for the consultation answer, the answer has arrived: the owner gets
    /// the follow-up action and any escalation raised while the answer was overdue is resolved.
    /// </summary>
    private bool TryCompleteAnswer(Case @case, PathwayDefinition pathway, DateTimeOffset at, out string? missing)
    {
        missing = null;
        if (!string.Equals(@case.NextAction, ConsultationTrack.AnswerAction, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!HandlerSupport.HasDeadline(@case, HandlerSupport.ArrangeFollowUpAction, pathway, out missing))
        {
            return false;
        }

        @case.ResolveEscalations(at);
        HandlerSupport.SetNextAction(@case, HandlerSupport.ArrangeFollowUpAction, pathway, _clock);
        return true;
    }

    private Task<EventHandlingResult> Propose(Case @case, ConsultationUpdatePayload payload, EventEnvelope envelope, PathwayDefinition pathway)
    {
        if (payload.ProposedOwner is null)
        {
            return HandlerSupport.Refused(@case, "A consultation that proposes a new owner must name the proposed owner.");
        }

        var proposedBy = envelope.Actor ?? new Actor
        {
            Reference = @case.Owner.Reference,
            Role = @case.Owner.Role,
            FacilityId = @case.Owner.FacilityId,
        };

        var verdict = OwnershipRules.TryPropose(@case, payload.ProposedOwner, proposedBy, pathway, _clock, out var proposal);
        if (!verdict.Allowed)
        {
            return HandlerSupport.Refused(@case, verdict.Reason!);
        }

        var track = @case.Consultation ?? @case.BeginConsultation(payload.ServiceRequestReference, envelope.OccurredAt);
        track.Advance(ConsultationStatus.OwnerProposed, envelope.OccurredAt, payload.TaskReference);
        @case.ProposeOwner(proposal!);

        return HandlerSupport.Applied(@case, payload.TaskReference);
    }

    private Task<EventHandlingResult> Accept(Case @case, ConsultationUpdatePayload payload, EventEnvelope envelope, PathwayDefinition pathway)
    {
        if (string.Equals(@case.NextAction, ConsultationTrack.AnswerAction, StringComparison.OrdinalIgnoreCase)
            && !HandlerSupport.HasDeadline(@case, HandlerSupport.ArrangeFollowUpAction, pathway, out var missing))
        {
            return HandlerSupport.Refused(@case, missing!);
        }

        var pending = @case.PendingAssignment;
        if (pending is not null)
        {
            var responder = Responder(envelope, pending);
            var verdict = OwnershipRules.CanRespond(@case, responder);
            if (!verdict.Allowed)
            {
                return HandlerSupport.Refused(@case, verdict.Reason!);
            }

            var track = @case.Consultation ?? @case.BeginConsultation(payload.ServiceRequestReference, envelope.OccurredAt);
            track.Advance(ConsultationStatus.Accepted, envelope.OccurredAt, payload.TaskReference);
            @case.AcceptOwnership(responder, envelope.OccurredAt);
            @case.ClearException(CaseExceptions.Rejected, envelope.OccurredAt);
        }
        else
        {
            var track = @case.Consultation ?? @case.BeginConsultation(payload.ServiceRequestReference, envelope.OccurredAt);
            track.Advance(ConsultationStatus.Accepted, envelope.OccurredAt, payload.TaskReference);
        }

        TryCompleteAnswer(@case, pathway, envelope.OccurredAt, out _);

        return HandlerSupport.Applied(@case, payload.TaskReference);
    }

    private Task<EventHandlingResult> Reject(Case @case, ConsultationUpdatePayload payload, EventEnvelope envelope, PathwayDefinition pathway)
    {
        var nextAction = HandlerSupport.ExceptionAction(pathway, TrackKind.Consultation);
        if (!HandlerSupport.HasDeadline(@case, nextAction, pathway, out var missing))
        {
            return HandlerSupport.Refused(@case, missing!);
        }

        var reason = string.IsNullOrWhiteSpace(payload.Reason) ? "No reason given by the source." : payload.Reason;
        var pending = @case.PendingAssignment;
        Actor? responder = null;
        if (pending is not null)
        {
            responder = Responder(envelope, pending);
            var verdict = OwnershipRules.CanRespond(@case, responder);
            if (!verdict.Allowed)
            {
                return HandlerSupport.Refused(@case, verdict.Reason!);
            }
        }

        var track = @case.Consultation ?? @case.BeginConsultation(payload.ServiceRequestReference, envelope.OccurredAt);
        track.Advance(ConsultationStatus.Rejected, envelope.OccurredAt, payload.TaskReference);

        if (responder is not null)
        {
            @case.RejectOwnership(responder, reason, envelope.OccurredAt);
        }
        else
        {
            @case.FlagException(CaseExceptions.Rejected, envelope.OccurredAt);
        }

        HandlerSupport.SetNextAction(@case, nextAction, pathway, _clock);

        return HandlerSupport.Applied(@case, payload.TaskReference);
    }

    private Task<EventHandlingResult> RecordOutcome(Case @case, ConsultationUpdatePayload payload, EventEnvelope envelope, PathwayDefinition pathway)
    {
        var track = @case.Consultation;
        if (track is null)
        {
            return HandlerSupport.Refused(@case, "No consultation was requested on this case, so no consultation outcome can be recorded.");
        }

        if (string.Equals(@case.NextAction, ConsultationTrack.AnswerAction, StringComparison.OrdinalIgnoreCase)
            && !HandlerSupport.HasDeadline(@case, HandlerSupport.ArrangeFollowUpAction, pathway, out var missing))
        {
            return HandlerSupport.Refused(@case, missing!);
        }

        track.Advance(ConsultationStatus.OutcomeRecorded, envelope.OccurredAt, payload.TaskReference);
        TryCompleteAnswer(@case, pathway, envelope.OccurredAt, out _);
        return HandlerSupport.Applied(@case, payload.TaskReference);
    }

    private static Actor Responder(EventEnvelope envelope, Assignment pending) =>
        envelope.Actor ?? new Actor
        {
            Reference = pending.ProposedOwner.Reference,
            Role = pending.ProposedOwner.Role,
            FacilityId = pending.ProposedOwner.FacilityId,
        };
}
