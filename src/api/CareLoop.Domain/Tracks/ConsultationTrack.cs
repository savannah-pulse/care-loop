namespace CareLoop.Domain.Tracks;

/// <summary>
/// The track for a consultation request and the ownership handoff it carries.
/// </summary>
/// <remarks>
/// <para>
/// <b>Definition.</b> A request for another clinician to take the case on. Statuses:
/// <see cref="ConsultationStatus.Requested"/>, <see cref="ConsultationStatus.OwnerProposed"/>,
/// <see cref="ConsultationStatus.Accepted"/>, <see cref="ConsultationStatus.Rejected"/>,
/// <see cref="ConsultationStatus.OutcomeRecorded"/>.
/// </para>
/// <para>
/// <b>Rules.</b> The track completes only when the consultation outcome is recorded. A rejection
/// returns the next action to the current owner.
/// </para>
/// <para>
/// <b>Why.</b> A consultation is how a case is handed to another clinician, which is where
/// responsibility is most easily dropped. An accepted request alone is not completed care.
/// </para>
/// </remarks>
public sealed class ConsultationTrack
{
    /// <summary>The current status of the track.</summary>
    public ConsultationStatus Status { get; private set; }

    /// <summary>Source-system reference of the resource the track follows.</summary>
    public string? SourceReference { get; private set; }

    /// <summary>When the track was started.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>When the status last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Moves the track to a new status reported by the source.
    /// </summary>
    /// <param name="status">The status reported by the source system.</param>
    /// <param name="at">When the change happened according to the source.</param>
    /// <param name="sourceReference">Reference to the source resource carrying the status.</param>
    /// <exception cref="InvalidOperationException">The change is not a valid move from the current status.</exception>
    public void Advance(ConsultationStatus status, DateTimeOffset at, string? sourceReference)
    {
        // TODO(WS-1): validate the status move, then update Status, SourceReference and UpdatedAt.
        throw new NotImplementedException("WS1: ConsultationTrack.Advance");
    }

    /// <summary>
    /// Tells whether the track has reached its completing status.
    /// </summary>
    /// <returns><see langword="true"/> only when the consultation outcome has been recorded.</returns>
    public bool IsComplete()
    {
        // TODO(WS-1): return whether Status is the completing status for this track.
        throw new NotImplementedException("WS1: ConsultationTrack.IsComplete");
    }
}
