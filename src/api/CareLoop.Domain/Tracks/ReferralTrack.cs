namespace CareLoop.Domain.Tracks;

/// <summary>
/// The track for a referral to another service.
/// </summary>
/// <remarks>
/// <para>
/// <b>Definition.</b> A referral sent to another service and that service's response. Statuses:
/// <see cref="ReferralStatus.Sent"/>, <see cref="ReferralStatus.Accepted"/>,
/// <see cref="ReferralStatus.Rejected"/>.
/// </para>
/// <para>
/// <b>Rules.</b> The track completes when the referral is accepted. A rejection creates a next
/// action for the current owner instead of parking the case.
/// </para>
/// <para>
/// <b>Why.</b> A sent or accepted referral is never counted as completed care. Acceptance completes
/// this track, but the case still needs an attended appointment and a recorded outcome before it
/// can be closed.
/// </para>
/// </remarks>
public sealed class ReferralTrack
{
    /// <summary>The current status of the track.</summary>
    public ReferralStatus Status { get; private set; }

    /// <summary>Source-system reference of the resource the track follows.</summary>
    public string? SourceReference { get; private set; }

    /// <summary>When the track was started.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>When the status last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The reason given when the receiving service rejected the referral.</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Moves the track to a new status reported by the source.
    /// </summary>
    /// <param name="status">The status reported by the source system.</param>
    /// <param name="at">When the change happened according to the source.</param>
    /// <param name="sourceReference">Reference to the source resource carrying the status.</param>
    /// <exception cref="InvalidOperationException">The change is not a valid move from the current status.</exception>
    public void Advance(ReferralStatus status, DateTimeOffset at, string? sourceReference)
    {
        // TODO(WS-1): validate the status move, then update Status, SourceReference and UpdatedAt.
        throw new NotImplementedException("WS1: ReferralTrack.Advance");
    }

    /// <summary>
    /// Tells whether the track has reached its completing status.
    /// </summary>
    /// <returns><see langword="true"/> when the receiving service accepted the referral.</returns>
    public bool IsComplete()
    {
        // TODO(WS-1): return whether Status is the completing status for this track.
        throw new NotImplementedException("WS1: ReferralTrack.IsComplete");
    }
}
