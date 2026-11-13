using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Applies a delivery report or a patient response to the notification track.
/// </summary>
/// <remarks>
/// Will record the attempt outcome (delivered if any channel delivers, failed only if all fail, then flag unreachable with a next action), complete the track only on acknowledgement or teach-back, and turn a call-me request into a next action for the current owner by raising <see cref="CareLoop.Domain.Events.DomainEvents.CallRequested"/>. Delivery never completes the track, transfers ownership or stops a clock.
/// </remarks>
public sealed class NotificationUpdateHandler : IEventHandler
{
    private readonly IPathwayProvider _pathways;
    private readonly IClock _clock;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="pathways">Supplies the pathway definition the rules are evaluated against.</param>
    /// <param name="clock">Supplies the current time for deadlines.</param>
    public NotificationUpdateHandler(IPathwayProvider pathways, IClock clock)
    {
        _pathways = pathways;
        _clock = clock;
    }

    /// <inheritdoc/>
    public CaseEventType EventType => CaseEventType.NotificationUpdated;

    /// <inheritdoc/>
    /// <remarks>Expects the envelope's payload to be a <see cref="NotificationUpdatePayload"/>.</remarks>
    public Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct)
    {
        // TODO(WS-2): update the Notification track; handle failed-if-all, acknowledgement, teach-back and call-me.
        throw new NotImplementedException("WS2: NotificationUpdateHandler.HandleAsync");
    }
}
