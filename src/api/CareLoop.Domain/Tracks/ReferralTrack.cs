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
/// action for the current owner instead of parking the case; the owner may then send the referral
/// again, to another service.
/// </para>
/// <para>
/// <b>Why.</b> A sent or accepted referral is never counted as completed care. Acceptance completes
/// this track, but the case still needs an attended appointment and a recorded outcome before it
/// can be closed.
/// </para>
/// </remarks>
public sealed class ReferralTrack
{
    /// <summary>
    /// Which statuses may follow which. A status not listed as a key is final.
    /// </summary>
    private static readonly IReadOnlyDictionary<ReferralStatus, ReferralStatus[]> AllowedMoves =
        new Dictionary<ReferralStatus, ReferralStatus[]>
        {
            [ReferralStatus.Sent] = [ReferralStatus.Accepted, ReferralStatus.Rejected],
            [ReferralStatus.Accepted] = [],
            [ReferralStatus.Rejected] = [ReferralStatus.Sent],
        };

    /// <summary>
    /// Starts the track when a referral is sent. Used by <see cref="Cases.Case.BeginReferral"/>.
    /// </summary>
    /// <param name="sourceReference">Reference to the referral <c>ServiceRequest</c>.</param>
    /// <param name="startedAt">When the referral was sent.</param>
    public ReferralTrack(string? sourceReference, DateTimeOffset startedAt)
    {
        Status = ReferralStatus.Sent;
        SourceReference = sourceReference;
        StartedAt = startedAt;
        UpdatedAt = startedAt;
    }

    /// <summary>
    /// Used only by the persistence layer when materializing a stored track.
    /// </summary>
    private ReferralTrack()
    {
    }

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
    /// <remarks>
    /// Reporting the current status again is accepted and only refreshes the reference and time, so
    /// a resend is harmless. Moving back to <see cref="ReferralStatus.Sent"/> after a rejection
    /// clears the recorded rejection reason. Use <see cref="Reject"/> to record a rejection with
    /// the reason the receiving service gave.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The change is not a valid move from the current status.</exception>
    public void Advance(ReferralStatus status, DateTimeOffset at, string? sourceReference)
    {
        if (status != Status && !AllowedMoves[Status].Contains(status))
        {
            throw new InvalidOperationException(
                $"The referral track cannot move from {Status} to {status}.");
        }

        if (status == ReferralStatus.Sent)
        {
            RejectionReason = null;
        }

        Status = status;
        SourceReference = sourceReference ?? SourceReference;
        UpdatedAt = at;
    }

    /// <summary>
    /// Records that the receiving service rejected the referral, and why.
    /// </summary>
    /// <param name="reason">The reason given by the receiving service.</param>
    /// <param name="at">When the rejection happened according to the source.</param>
    /// <param name="sourceReference">Reference to the source resource carrying the rejection.</param>
    /// <remarks>The reason is kept so the owner arranging the alternative, and the audit trail, can see why the first referral failed.</remarks>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The referral cannot be rejected from its current status.</exception>
    public void Reject(string reason, DateTimeOffset at, string? sourceReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Advance(ReferralStatus.Rejected, at, sourceReference);
        RejectionReason = reason;
    }

    /// <summary>
    /// Tells whether the track has reached its completing status.
    /// </summary>
    /// <returns><see langword="true"/> when the receiving service accepted the referral.</returns>
    public bool IsComplete() => Status == ReferralStatus.Accepted;
}
