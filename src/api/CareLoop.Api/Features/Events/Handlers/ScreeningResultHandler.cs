using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Opens a case for a new actionable screening result.
/// </summary>
/// <remarks>
/// Will create the <see cref="Case"/> with the owner chosen by <see cref="CareLoop.Domain.Rules.OwnershipRules.InitialOwner"/> (the facility lead queue when nobody is named, flagging it unassigned), move it to review pending, and set the first next action and deadline from the pathway definition. A result for which a case already exists is refused rather than opening a second case.
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
        // TODO(WS-2): open the case, assign the initial owner, set stage, next action and due time from the pathway.
        throw new NotImplementedException("WS2: ScreeningResultHandler.HandleAsync");
    }
}
