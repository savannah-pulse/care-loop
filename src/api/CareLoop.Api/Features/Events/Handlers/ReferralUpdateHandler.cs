using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Applies a referral status change.
/// </summary>
/// <remarks>
/// Will start or advance the referral track. An accepted referral completes the track but is never counted as completed care, so the case stays in follow-up. A rejected referral raises <see cref="CareLoop.Domain.Events.DomainEvents.ReferralRejected"/>, flags the exception and gives the current owner a new next action and deadline.
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
        // TODO(WS-2): advance the Referral track; on rejection flag the exception and set a new next action.
        throw new NotImplementedException("WS2: ReferralUpdateHandler.HandleAsync");
    }
}
