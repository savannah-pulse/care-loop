using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Applies a consultation status change, including the ownership handoff it carries.
/// </summary>
/// <remarks>
/// Will start or advance the consultation track; on a proposed owner use <see cref="CareLoop.Domain.Rules.OwnershipRules.TryPropose"/> (the current owner stays accountable); on acceptance transfer ownership; on rejection return the next action to the current owner with a new deadline and flag the exception.
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
        // TODO(WS-2): advance the Consultation track and run the propose/accept/reject ownership rules.
        throw new NotImplementedException("WS2: ConsultationUpdateHandler.HandleAsync");
    }
}
