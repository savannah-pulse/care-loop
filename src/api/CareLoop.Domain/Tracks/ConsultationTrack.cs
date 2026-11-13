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
/// returns the next action to the current owner, who may request the consultation again.
/// </para>
/// <para>
/// <b>Why.</b> A consultation is how a case is handed to another clinician, which is where
/// responsibility is most easily dropped. An accepted request alone is not completed care.
/// </para>
/// </remarks>
public sealed class ConsultationTrack
{
    /// <summary>
    /// Key of the next action the case carries while a consultation awaits an answer; names the
    /// answer deadline in the pathway's deadlines. Also the deadline a proposed owner has to respond
    /// to a handoff, since a handoff is carried by a consultation.
    /// </summary>
    /// <remarks>This is a key into the pathway definition, not a duration: how long the answer may take is configuration.</remarks>
    public const string AnswerAction = "answer-consultation";

    /// <summary>
    /// Which statuses may follow which. A status not listed as a key is final.
    /// </summary>
    private static readonly IReadOnlyDictionary<ConsultationStatus, ConsultationStatus[]> AllowedMoves =
        new Dictionary<ConsultationStatus, ConsultationStatus[]>
        {
            [ConsultationStatus.Requested] = [ConsultationStatus.OwnerProposed, ConsultationStatus.Accepted, ConsultationStatus.Rejected],
            [ConsultationStatus.OwnerProposed] = [ConsultationStatus.Accepted, ConsultationStatus.Rejected],
            [ConsultationStatus.Accepted] = [ConsultationStatus.OutcomeRecorded],
            [ConsultationStatus.Rejected] = [ConsultationStatus.Requested, ConsultationStatus.OwnerProposed],
            [ConsultationStatus.OutcomeRecorded] = [],
        };

    /// <summary>
    /// Starts the track when a consultation is requested. Used by <see cref="Cases.Case.BeginConsultation"/>.
    /// </summary>
    /// <param name="sourceReference">Reference to the consultation <c>ServiceRequest</c>.</param>
    /// <param name="startedAt">When the consultation was requested.</param>
    public ConsultationTrack(string? sourceReference, DateTimeOffset startedAt)
    {
        Status = ConsultationStatus.Requested;
        SourceReference = sourceReference;
        StartedAt = startedAt;
        UpdatedAt = startedAt;
    }

    /// <summary>
    /// Used only by the persistence layer when materializing a stored track.
    /// </summary>
    private ConsultationTrack()
    {
    }

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
    /// <remarks>Reporting the current status again is accepted and only refreshes the reference and time, so a resend is harmless.</remarks>
    /// <exception cref="InvalidOperationException">The change is not a valid move from the current status.</exception>
    public void Advance(ConsultationStatus status, DateTimeOffset at, string? sourceReference)
    {
        if (status != Status && !AllowedMoves[Status].Contains(status))
        {
            throw new InvalidOperationException(
                $"The consultation track cannot move from {Status} to {status}.");
        }

        Status = status;
        SourceReference = sourceReference ?? SourceReference;
        UpdatedAt = at;
    }

    /// <summary>
    /// Tells whether the track has reached its completing status.
    /// </summary>
    /// <returns><see langword="true"/> only when the consultation outcome has been recorded.</returns>
    public bool IsComplete() => Status == ConsultationStatus.OutcomeRecorded;
}
