using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Rules;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Applies a completed clinical review.
/// </summary>
/// <remarks>
/// Checks <see cref="TransitionRules"/>, stores the reviewed urgency exactly as received (CareLoop
/// never sets or changes urgency), moves the case to follow-up active, requests the patient
/// notification and sets the next action and deadline for that urgency. Completing the review also
/// resolves any escalation raised because the review was overdue. An urgency code the pathway does
/// not know is refused.
/// </remarks>
public sealed class ClinicalReviewHandler : IEventHandler
{
    /// <summary>Key of the approved patient template sent when a result has been reviewed.</summary>
    public const string PatientFollowUpTemplate = "patient-follow-up";

    private readonly IPathwayProvider _pathways;
    private readonly IClock _clock;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="pathways">Supplies the pathway definition the rules are evaluated against.</param>
    /// <param name="clock">Supplies the current time for deadlines.</param>
    public ClinicalReviewHandler(IPathwayProvider pathways, IClock clock)
    {
        _pathways = pathways;
        _clock = clock;
    }

    /// <inheritdoc/>
    public CaseEventType EventType => CaseEventType.ClinicalReviewCompleted;

    /// <inheritdoc/>
    /// <remarks>Expects the envelope's payload to be a <see cref="ClinicalReviewPayload"/>.</remarks>
    public Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct)
    {
        var payload = HandlerSupport.Payload<ClinicalReviewPayload>(envelope);
        var @case = HandlerSupport.RequireCase(existing, EventType);
        var pathway = _pathways.Get(@case.PathwayId);

        var transition = TransitionRules.Evaluate(@case, EventType, envelope.Actor, pathway);
        if (!transition.Verdict.Allowed)
        {
            return HandlerSupport.Refused(@case, transition.Verdict.Reason!);
        }

        if (!pathway.UrgencyCodes.Contains(payload.UrgencyCode, StringComparer.Ordinal))
        {
            return HandlerSupport.Refused(@case,
                $"Urgency code '{payload.UrgencyCode}' is not one the pathway '{pathway.Id}' knows; CareLoop cannot interpret it.");
        }

        var reviewer = HandlerSupport.ActorOrSource(envelope);
        @case.RecordReview(payload.UrgencyCode, reviewer, envelope.OccurredAt);

        if (transition.NextAction is { } nextAction && !HandlerSupport.HasDeadline(@case, nextAction, pathway, out var missing))
        {
            throw new InvalidOperationException(missing);
        }

        if (transition.NextStage is { } next)
        {
            @case.MoveTo(next, envelope.OccurredAt);
        }

        @case.ResolveEscalations(envelope.OccurredAt);
        @case.BeginNotification(PatientFollowUpTemplate, _clock.UtcNow);

        if (transition.NextAction is { } action)
        {
            HandlerSupport.SetNextAction(@case, action, pathway, _clock);
        }

        return HandlerSupport.Applied(@case, payload.ReviewTaskReference);
    }
}
