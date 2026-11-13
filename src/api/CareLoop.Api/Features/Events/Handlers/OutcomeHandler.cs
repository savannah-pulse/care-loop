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
/// Refuses an outcome whose <see cref="OutcomePayload.EvidenceKind"/> the pathway does not declare
/// (C21), so an unrecognised outcome never counts as evidence. Otherwise checks
/// <see cref="TransitionRules"/>, stores the evidence reference and kind on the case and, from
/// follow-up, moves it to closure review with a next action for the authorized closer. A further
/// outcome during closure review adds evidence without leaving the stage. When the evidence kind
/// names a next action of its own (a treatment plan leads to <c>start-treatment</c>), that action
/// and its deadline replace the stage's. Completing follow-up resolves any open escalation.
/// Recording an outcome never closes the case by itself.
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

        var kind = pathway.EvidenceKinds.FirstOrDefault(k => string.Equals(k.Kind, payload.EvidenceKind, StringComparison.OrdinalIgnoreCase));
        if (kind is null)
        {
            var known = string.Join(", ", pathway.EvidenceKinds.Select(k => k.Kind));
            return HandlerSupport.Refused(@case,
                $"Evidence kind '{payload.EvidenceKind}' is not one the pathway '{pathway.Id}' declares ({known}); the outcome was not recorded.");
        }

        var transition = TransitionRules.Evaluate(@case, EventType, envelope.Actor, pathway);
        if (!transition.Verdict.Allowed)
        {
            return HandlerSupport.Refused(@case, transition.Verdict.Reason!);
        }

        var nextAction = kind.NextAction ?? transition.NextAction;
        if (nextAction is not null && !HandlerSupport.HasDeadline(@case, nextAction, pathway, out var missing))
        {
            return HandlerSupport.Refused(@case, missing!);
        }

        @case.RecordOutcomeEvidence(payload.EvidenceReference, envelope.OccurredAt, kind.Kind);

        if (transition.NextStage is { } next)
        {
            @case.MoveTo(next, envelope.OccurredAt);
        }

        @case.ResolveEscalations(envelope.OccurredAt);

        if (nextAction is not null)
        {
            HandlerSupport.SetNextAction(@case, nextAction, pathway, _clock);
        }

        return HandlerSupport.Applied(@case, payload.EvidenceReference);
    }
}
