using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Rules;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Applies recorded outcome evidence.
/// </summary>
/// <remarks>
/// Checks <see cref="TransitionRules"/>, stores the evidence reference on the case and moves it to
/// closure review with a next action for the authorized closer. Completing follow-up resolves any
/// open escalation. Recording an outcome never closes the case by itself.
/// </remarks>
public sealed class OutcomeHandler : IEventHandler
{
    private readonly IPathwayProvider _pathways;
    private readonly IClock _clock;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="pathways">Supplies the pathway definition the rules are evaluated against.</param>
    /// <param name="clock">Supplies the current time for deadlines.</param>
    public OutcomeHandler(IPathwayProvider pathways, IClock clock)
    {
        _pathways = pathways;
        _clock = clock;
    }

    /// <inheritdoc/>
    public CaseEventType EventType => CaseEventType.OutcomeRecorded;

    /// <inheritdoc/>
    /// <remarks>Expects the envelope's payload to be an <see cref="OutcomePayload"/>.</remarks>
    public Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct)
    {
        var payload = HandlerSupport.Payload<OutcomePayload>(envelope);
        var @case = HandlerSupport.RequireCase(existing, EventType);
        var pathway = _pathways.Get(@case.PathwayId);

        var transition = TransitionRules.Evaluate(@case, EventType, envelope.Actor, pathway);
        if (!transition.Verdict.Allowed)
        {
            return HandlerSupport.Refused(@case, transition.Verdict.Reason!);
        }

        if (transition.NextAction is { } nextAction && !HandlerSupport.HasDeadline(@case, nextAction, pathway, out var missing))
        {
            return HandlerSupport.Refused(@case, missing!);
        }

        @case.RecordOutcomeEvidence(payload.EvidenceReference, envelope.OccurredAt);

        if (transition.NextStage is { } next)
        {
            @case.MoveTo(next, envelope.OccurredAt);
        }

        @case.ResolveEscalations(envelope.OccurredAt);

        if (transition.NextAction is { } action)
        {
            HandlerSupport.SetNextAction(@case, action, pathway, _clock);
        }

        return HandlerSupport.Applied(@case, payload.EvidenceReference);
    }
}
