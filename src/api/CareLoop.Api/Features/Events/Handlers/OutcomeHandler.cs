using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Records outcome evidence and moves the case to closure review.
/// </summary>
/// <remarks>
/// Will check <see cref="CareLoop.Domain.Rules.TransitionRules"/>, store the evidence reference on the case and move it to closure review with a next action for the authorized closer. Recording an outcome never closes the case by itself.
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
    /// <remarks>Expects the envelope's payload to be a <see cref="OutcomePayload"/>.</remarks>
    public Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct)
    {
        // TODO(WS-2): record the evidence reference, move to ClosureReview and set the closure-decision deadline.
        throw new NotImplementedException("WS2: OutcomeHandler.HandleAsync");
    }
}
