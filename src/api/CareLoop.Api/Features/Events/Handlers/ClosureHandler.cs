using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Applies an authorized human closure decision, or refuses it.
/// </summary>
/// <remarks>
/// Will build a <see cref="ClosureDecision"/> from the payload and the envelope&apos;s actor and pass it to <see cref="CareLoop.Domain.Rules.ClosureGate"/>. Only an allowed decision closes the case. Anything else (no authorizer, wrong role, missing evidence or exception documentation) is returned as not applied with the gate&apos;s reason, so the ingestor stores and audits the attempt and the case stays open.
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
        // TODO(WS-2): evaluate ClosureGate; close on allow, otherwise return a rejected result with the reason.
        throw new NotImplementedException("WS2: ClosureHandler.HandleAsync");
    }
}
