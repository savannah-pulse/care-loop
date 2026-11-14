using System.Collections.Concurrent;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Remembers which domain events this process has already sent notifications for.
/// </summary>
/// <remarks>
/// <para>
/// The outbox retries a delivery only when the router failed, and the router does its sending last,
/// so a repeat is rare. This log stops one anyway: a domain event whose notifications were sent is
/// never sent again by the same process.
/// </para>
/// <para>
/// It lives in memory, so it does not survive a restart. A crash between sending and the outbox
/// recording the delivery can still repeat a send after a restart. Attempt identifiers are derived
/// from the event (<see cref="NotificationSender.AttemptIdFor"/>), so such a repeat carries the same
/// identifiers and the receiving side can drop it. Closing that window entirely needs a durable
/// record of sent notifications in the coordination store.
/// </para>
/// </remarks>
public sealed class NotificationSendLog
{
    /// <summary>How many event identifiers are remembered before the oldest are forgotten.</summary>
    public const int Capacity = 100_000;

    private readonly ConcurrentDictionary<Guid, byte> _sent = new();
    private readonly ConcurrentQueue<Guid> _order = new();

    /// <summary>
    /// Tells whether notifications for an event were already sent.
    /// </summary>
    /// <param name="eventId">The domain event identifier.</param>
    /// <returns><see langword="true"/> when <see cref="MarkSent"/> was called for it.</returns>
    public bool WasSent(Guid eventId) => _sent.ContainsKey(eventId);

    /// <summary>
    /// Records that notifications for an event were sent.
    /// </summary>
    /// <param name="eventId">The domain event identifier.</param>
    public void MarkSent(Guid eventId)
    {
        if (!_sent.TryAdd(eventId, 0))
        {
            return;
        }

        _order.Enqueue(eventId);
        while (_order.Count > Capacity && _order.TryDequeue(out var oldest))
        {
            _sent.TryRemove(oldest, out _);
        }
    }
}
