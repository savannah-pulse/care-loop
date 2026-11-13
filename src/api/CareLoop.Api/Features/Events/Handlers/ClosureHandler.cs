using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Rules;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Applies an authorized human closure decision, or refuses it.
/// </summary>
/// <remarks>
/// Builds a <see cref="ClosureDecision"/> from the payload and the envelope's actor and passes it to
/// <see cref="ClosureGate"/>, then to <see cref="TransitionRules"/>. Only a decision both allow
/// closes the case. Anything else (no authorizer, wrong role, missing evidence or exception
/// documentation, wrong stage) is returned as not applied with the rule's reason, so the ingestor
/// stores and audits the attempt and the case stays open.
/// </remarks>
public sealed class ClosureHandler : IEventHandler
{
    private readonly IPathwayProvider _pathways;
    private readonly IClock _clock;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="pathways">Supplies the pathway definition the rules are evaluated against.</param>
    /// <param name="clock">Supplies the current time for deadlines.</param>
    public ClosureHandler(IPathwayProvider pathways, IClock clock)
    {
        _pathways = pathways;
        _clock = clock;
    }

    /// <inheritdoc/>
    public CaseEventType EventType => CaseEventType.ClosureDecided;

    /// <inheritdoc/>
    /// <remarks>Expects the envelope's payload to be a <see cref="ClosurePayload"/>.</remarks>
    public Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct)
    {
        var payload = HandlerSupport.Payload<ClosurePayload>(envelope);
        var @case = HandlerSupport.RequireCase(existing, EventType);
        var pathway = _pathways.Get(@case.PathwayId);

        if (envelope.Actor is null)
        {
            return HandlerSupport.Refused(@case, "Closure needs a human: the closure names no authorizer.");
        }

        var decision = new ClosureDecision
        {
            Reason = payload.Reason,
            AuthorizedBy = envelope.Actor,
            DecidedAt = envelope.OccurredAt,
            EvidenceReference = payload.EvidenceReference,
            ExceptionNote = payload.ExceptionNote,
            NextSurveillanceDate = payload.NextSurveillanceDate,
            DocumentedAttemptReferences = payload.DocumentedAttemptReferences,
        };

        var gate = ClosureGate.Evaluate(@case, decision, pathway);
        if (!gate.Allowed)
        {
            return HandlerSupport.Refused(@case, gate.Reason!);
        }

        var transition = TransitionRules.Evaluate(@case, EventType, envelope.Actor, pathway);
        if (!transition.Verdict.Allowed)
        {
            return HandlerSupport.Refused(@case, transition.Verdict.Reason!);
        }

        @case.Close(decision, _clock.UtcNow);

        return HandlerSupport.Applied(@case, payload.EvidenceReference);
    }
}
