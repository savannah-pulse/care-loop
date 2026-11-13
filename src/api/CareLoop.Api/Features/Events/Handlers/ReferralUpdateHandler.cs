using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Rules;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Applies a referral status change.
/// </summary>
/// <remarks>
/// Starts or advances the referral track. An accepted referral completes the track but is never
/// counted as completed care, so the case stays in follow-up with its next action unchanged. A
/// rejected referral raises <see cref="ReferralRejected"/>, flags the exception and gives the
/// current owner a new next action and deadline. A referral reported as sent again after a
/// rejection clears the exception. A sent referral gives the case the
/// <see cref="ReferralTrack.RespondAction"/> next action with the pathway's response deadline;
/// the receiving service's acceptance completes that action, resolves any escalation raised while
/// it was overdue and hands the owner the follow-up action. A referral reported as sent while it is
/// already sent changes nothing, so a resend never restarts the response deadline.
/// </remarks>
public sealed class ReferralUpdateHandler : IEventHandler
{
    private readonly IPathwayProvider _pathways;
    private readonly IClock _clock;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="pathways">Supplies the pathway definition the rules are evaluated against.</param>
    /// <param name="clock">Supplies the current time for deadlines.</param>
    public ReferralUpdateHandler(IPathwayProvider pathways, IClock clock)
    {
        _pathways = pathways;
        _clock = clock;
    }

    /// <inheritdoc/>
    public CaseEventType EventType => CaseEventType.ReferralUpdated;

    /// <inheritdoc/>
    /// <remarks>Expects the envelope's payload to be a <see cref="ReferralUpdatePayload"/>.</remarks>
    public Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct)
    {
        var payload = HandlerSupport.Payload<ReferralUpdatePayload>(envelope);
        var @case = HandlerSupport.RequireCase(existing, EventType);
        var pathway = _pathways.Get(@case.PathwayId);

        var transition = TransitionRules.Evaluate(@case, EventType, envelope.Actor, pathway);
        if (!transition.Verdict.Allowed)
        {
            return HandlerSupport.Refused(@case, transition.Verdict.Reason!);
        }

        switch (payload.Status)
        {
            case ReferralStatus.Sent:
                if (!HandlerSupport.HasDeadline(@case, ReferralTrack.RespondAction, pathway, out var noResponseDeadline))
                {
                    return HandlerSupport.Refused(@case, noResponseDeadline!);
                }

                // A repeat of the referral already sent (the ServiceRequest, then its Task, and every resend) is
                // recorded but must not restart the response deadline, or a resend would hide an overdue reply.
                if (@case.Referral is { Status: ReferralStatus.Sent })
                {
                    break;
                }

                if (@case.Referral is { } sent)
                {
                    sent.Advance(ReferralStatus.Sent, envelope.OccurredAt, payload.ServiceRequestReference);
                    @case.ClearException(CaseExceptions.Rejected, envelope.OccurredAt);
                }
                else
                {
                    @case.BeginReferral(payload.ServiceRequestReference, envelope.OccurredAt);
                }

                HandlerSupport.SetNextAction(@case, ReferralTrack.RespondAction, pathway, _clock);
                break;

            case ReferralStatus.Accepted:
                var awaitingResponse = string.Equals(@case.NextAction, ReferralTrack.RespondAction, StringComparison.OrdinalIgnoreCase);
                if (awaitingResponse && !HandlerSupport.HasDeadline(@case, HandlerSupport.ArrangeFollowUpAction, pathway, out var noFollowUpDeadline))
                {
                    return HandlerSupport.Refused(@case, noFollowUpDeadline!);
                }

                var accepted = @case.Referral ?? @case.BeginReferral(payload.ServiceRequestReference, envelope.OccurredAt);
                accepted.Advance(ReferralStatus.Accepted, envelope.OccurredAt, payload.TaskReference);
                @case.ClearException(CaseExceptions.Rejected, envelope.OccurredAt);
                if (awaitingResponse)
                {
                    @case.ResolveEscalations(envelope.OccurredAt);
                    HandlerSupport.SetNextAction(@case, HandlerSupport.ArrangeFollowUpAction, pathway, _clock);
                }

                break;

            case ReferralStatus.Rejected:
                var nextAction = HandlerSupport.ExceptionAction(pathway, TrackKind.Referral);
                if (!HandlerSupport.HasDeadline(@case, nextAction, pathway, out var missing))
                {
                    return HandlerSupport.Refused(@case, missing!);
                }

                var reason = string.IsNullOrWhiteSpace(payload.Reason) ? "No reason given by the receiving service." : payload.Reason;
                var rejected = @case.Referral ?? @case.BeginReferral(payload.ServiceRequestReference, envelope.OccurredAt);
                rejected.Reject(reason, envelope.OccurredAt, payload.TaskReference);
                @case.FlagException(CaseExceptions.Rejected, envelope.OccurredAt);
                HandlerSupport.SetNextAction(@case, nextAction, pathway, _clock);
                @case.Raise(new ReferralRejected
                {
                    EventId = Guid.NewGuid(),
                    CaseId = @case.Id,
                    OccurredAt = _clock.UtcNow,
                    CurrentOwner = @case.Owner,
                    Reason = reason,
                });
                break;

            default:
                return HandlerSupport.Refused(@case, $"Referral status {payload.Status} is not understood.");
        }

        return HandlerSupport.Applied(@case, payload.TaskReference);
    }
}
