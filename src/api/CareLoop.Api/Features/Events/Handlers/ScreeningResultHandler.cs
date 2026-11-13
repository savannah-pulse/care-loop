using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Rules;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Opens a case for a new actionable screening result.
/// </summary>
/// <remarks>
/// Creates the <see cref="Case"/> with the owner chosen by <see cref="OwnershipRules.InitialOwner"/>
/// (the facility lead queue when nobody is named, flagging it unassigned), moves it to review pending
/// and sets the first next action and deadline from the pathway definition. A queue-owned case gets
/// the Received stage's own next action (assigning an owner) because nobody can review a result
/// nobody owns; a case with a named owner gets the review-pending stage's. A result for which a case
/// already exists, or that names an unknown pathway, is refused rather than opening a second case.
/// The patient's language and contact points in the payload are not stored: a case holds no contact
/// data, and the notification feature resolves recipients when it sends.
/// </remarks>
public sealed class ScreeningResultHandler : IEventHandler
{
    private readonly IPathwayProvider _pathways;
    private readonly IClock _clock;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="pathways">Supplies the pathway definition the rules are evaluated against.</param>
    /// <param name="clock">Supplies the current time for deadlines.</param>
    public ScreeningResultHandler(IPathwayProvider pathways, IClock clock)
    {
        _pathways = pathways;
        _clock = clock;
    }

    /// <inheritdoc/>
    public CaseEventType EventType => CaseEventType.ScreeningResultReceived;

    /// <inheritdoc/>
    /// <remarks>Expects the envelope's payload to be a <see cref="ScreeningResultPayload"/>.</remarks>
    public Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct)
    {
        var payload = HandlerSupport.Payload<ScreeningResultPayload>(envelope);

        if (existing is not null)
        {
            return HandlerSupport.Refused(existing,
                $"A case already exists for result '{envelope.SourceSystem}/{envelope.SourceResultId}'; one result opens one case.");
        }

        PathwayDefinition pathway;
        try
        {
            pathway = _pathways.Get(payload.PathwayId);
        }
        catch (KeyNotFoundException ex)
        {
            throw new InvalidOperationException($"Screening result names unknown pathway '{payload.PathwayId}'.", ex);
        }

        var owner = OwnershipRules.InitialOwner(payload.FacilityId, payload.NamedOwner, pathway);
        var @case = new Case(
            Guid.NewGuid(),
            envelope.SourceSystem,
            envelope.SourceResultId,
            payload.PatientReference,
            payload.PathwayId,
            payload.FacilityId,
            owner,
            _clock.UtcNow);

        var transition = TransitionRules.Evaluate(@case, EventType, HandlerSupport.ActorOrSource(envelope), pathway);
        if (!transition.Verdict.Allowed)
        {
            throw new InvalidOperationException(transition.Verdict.Reason);
        }

        var receivedStage = pathway.Stages.First(s => s.Stage == CaseStage.Received);
        var nextAction = (owner.IsQueue ? receivedStage.NextActionOnEntry : null) ?? transition.NextAction;

        if (transition.NextStage is { } next)
        {
            @case.MoveTo(next, envelope.OccurredAt);
        }

        if (nextAction is not null)
        {
            HandlerSupport.SetNextAction(@case, nextAction, pathway, _clock);
        }

        return HandlerSupport.Applied(@case, payload.ResultReference);
    }
}
