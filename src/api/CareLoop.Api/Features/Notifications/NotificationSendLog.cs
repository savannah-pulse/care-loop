using System.Collections.Concurrent;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Remembers which notification attempts this process has already had accepted by a provider.
/// </summary>
/// <remarks>
/// <para>
/// The router makes the outbox retry an event when a staff or caregiver send failed. Keyed by attempt
/// identifier (one recipient on one channel), this log makes that retry resend only the channels that
/// failed: an attempt a provider accepted is never sent again by the same process.
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
    /// <summary>How many attempt identifiers are remembered before the oldest are forgotten.</summary>
    public const int Capacity = 100_000;

    private readonly ConcurrentDictionary<Guid, byte> _sent = new();
    private readonly ConcurrentQueue<Guid> _order = new();

    /// <summary>
    /// Tells whether an attempt was already accepted by its provider.
    /// </summary>
    /// <param name="attemptId">The attempt identifier (<see cref="NotificationSender.AttemptIdFor"/>).</param>
    /// <returns><see langword="true"/> when <see cref="MarkSent"/> was called for it.</returns>
    public bool WasSent(Guid attemptId) => _sent.ContainsKey(attemptId);

    /// <summary>
    /// Records that a provider accepted an attempt.
    /// </summary>
    /// <param name="attemptId">The attempt identifier.</param>
    public void MarkSent(Guid attemptId)
    {
        if (!_sent.TryAdd(attemptId, 0))
        {
            return;
        }

        _order.Enqueue(attemptId);
        while (_order.Count > Capacity && _order.TryDequeue(out var oldest))
        {
            _sent.TryRemove(oldest, out _);
        }
    }
}
