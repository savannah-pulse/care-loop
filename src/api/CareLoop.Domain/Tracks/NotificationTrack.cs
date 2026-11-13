namespace CareLoop.Domain.Tracks;

/// <summary>
/// The track for telling the patient about their result and getting their acknowledgement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Definition.</b> One notification to the patient, made up of one <see cref="NotificationAttempt"/>
/// per channel it was sent on. Statuses: <see cref="NotificationStatus.Sent"/>,
/// <see cref="NotificationStatus.Delivered"/>, <see cref="NotificationStatus.Failed"/>,
/// <see cref="NotificationStatus.Acknowledged"/>, <see cref="NotificationStatus.Understood"/>.
/// </para>
/// <para>
/// <b>Rules.</b> The notification is delivered if any channel delivers and failed only if all
/// channels fail. An acknowledgement on any channel acknowledges it. The track completes only when
/// the patient acknowledges.
/// </para>
/// <para>
/// <b>Why.</b> The track exists so that "we sent a message" is never mistaken for "the patient
/// knows". Delivery alone does not complete it, and notifying never transfers ownership or stops a
/// deadline clock.
/// </para>
/// </remarks>
public sealed class NotificationTrack
{
    /// <summary>The current status of the track.</summary>
    public NotificationStatus Status { get; private set; }

    /// <summary>Source-system reference of the resource the track follows.</summary>
    public string? SourceReference { get; private set; }

    /// <summary>When the track was started.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>When the status last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<NotificationAttempt> _attempts = [];

    /// <summary>Every send made for this notification, one per channel per send.</summary>
    public IReadOnlyList<NotificationAttempt> Attempts => _attempts;

    /// <summary><see langword="true"/> when the patient has asked to be called; the request is a next action for the owner, not an acknowledgement.</summary>
    public bool CallRequested { get; private set; }

    /// <summary>
    /// Records one send on one channel and recomputes the overall status.
    /// </summary>
    /// <param name="attempt">The attempt to record or, when its identifier is already known, to update.</param>
    /// <remarks>The notification is delivered if any attempt is delivered and failed only if all attempts failed.</remarks>
    public void RecordAttempt(NotificationAttempt attempt)
    {
        // TODO(WS-1): add or replace the attempt, then derive Status (delivered-if-any, failed-if-all).
        throw new NotImplementedException("WS1: NotificationTrack.RecordAttempt");
    }

    /// <summary>
    /// Moves the track to a new status reported by the source.
    /// </summary>
    /// <param name="status">The status reported by the source system.</param>
    /// <param name="at">When the change happened according to the source.</param>
    /// <param name="sourceReference">Reference to the source resource carrying the status.</param>
    /// <exception cref="InvalidOperationException">The change is not a valid move from the current status.</exception>
    public void Advance(NotificationStatus status, DateTimeOffset at, string? sourceReference)
    {
        // TODO(WS-1): validate the status move, then update Status, SourceReference and UpdatedAt.
        throw new NotImplementedException("WS1: NotificationTrack.Advance");
    }

    /// <summary>
    /// Tells whether the track has reached its completing status.
    /// </summary>
    /// <returns><see langword="true"/> only when the patient has acknowledged or confirmed understanding.</returns>
    public bool IsComplete()
    {
        // TODO(WS-1): return whether Status is the completing status for this track.
        throw new NotImplementedException("WS1: NotificationTrack.IsComplete");
    }
}
