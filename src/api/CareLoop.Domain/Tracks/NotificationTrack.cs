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
/// the patient acknowledges. A status never moves backwards: once the patient has acknowledged, a
/// late delivery report or failure on another channel cannot undo that.
/// </para>
/// <para>
/// <b>Why.</b> The track exists so that "we sent a message" is never mistaken for "the patient
/// knows". Delivery alone does not complete it, and notifying never transfers ownership or stops a
/// deadline clock.
/// </para>
/// </remarks>
public sealed class NotificationTrack
{
    /// <summary>
    /// Which statuses may follow which. A status not listed as a key is final.
    /// </summary>
    private static readonly IReadOnlyDictionary<NotificationStatus, NotificationStatus[]> AllowedMoves =
        new Dictionary<NotificationStatus, NotificationStatus[]>
        {
            [NotificationStatus.Sent] = [NotificationStatus.Delivered, NotificationStatus.Failed, NotificationStatus.Acknowledged, NotificationStatus.Understood],
            [NotificationStatus.Delivered] = [NotificationStatus.Acknowledged, NotificationStatus.Understood],
            [NotificationStatus.Failed] = [NotificationStatus.Delivered, NotificationStatus.Acknowledged, NotificationStatus.Understood],
            [NotificationStatus.Acknowledged] = [NotificationStatus.Understood],
            [NotificationStatus.Understood] = [],
        };

    private readonly List<NotificationAttempt> _attempts = [];

    /// <summary>
    /// Starts the track when a notification is requested. Used by <see cref="Cases.Case.BeginNotification"/>.
    /// </summary>
    /// <param name="startedAt">When the notification was requested.</param>
    /// <param name="sourceReference">Reference of the resource the track follows, when there is one yet.</param>
    public NotificationTrack(DateTimeOffset startedAt, string? sourceReference = null)
    {
        Status = NotificationStatus.Sent;
        SourceReference = sourceReference;
        StartedAt = startedAt;
        UpdatedAt = startedAt;
    }

    /// <summary>
    /// Used only by the persistence layer when materializing a stored track.
    /// </summary>
    private NotificationTrack()
    {
    }

    /// <summary>The current status of the track.</summary>
    public NotificationStatus Status { get; private set; }

    /// <summary>Source-system reference of the resource the track follows.</summary>
    public string? SourceReference { get; private set; }

    /// <summary>When the track was started.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>When the status last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Every send made for this notification, one per channel per send.</summary>
    public IReadOnlyList<NotificationAttempt> Attempts => _attempts;

    /// <summary><see langword="true"/> when the patient has asked to be called; the request is a next action for the owner, not an acknowledgement.</summary>
    public bool CallRequested { get; private set; }

    /// <summary>The lowest rating the patient may give for how confident they feel about what to do next.</summary>
    public const int MinimumConfidence = 1;

    /// <summary>The highest rating the patient may give for how confident they feel about what to do next.</summary>
    public const int MaximumConfidence = 5;

    /// <summary>
    /// How confident the patient said they feel about what to do next, from <see cref="MinimumConfidence"/>
    /// to <see cref="MaximumConfidence"/>, or <see langword="null"/> until they rate it.
    /// </summary>
    public int? Confidence { get; private set; }

    /// <summary>
    /// Records one send on one channel and recomputes the overall status.
    /// </summary>
    /// <param name="attempt">The attempt to record or, when its identifier is already known, to update.</param>
    /// <remarks>
    /// The notification is delivered if any attempt is delivered and failed only if all attempts
    /// failed. An attempt never moves the track back from <see cref="NotificationStatus.Acknowledged"/>
    /// or <see cref="NotificationStatus.Understood"/>: what the patient has confirmed stands.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="attempt"/> is <see langword="null"/>.</exception>
    public void RecordAttempt(NotificationAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        var index = _attempts.FindIndex(a => a.Id == attempt.Id);
        if (index >= 0)
        {
            _attempts[index] = attempt;
        }
        else
        {
            _attempts.Add(attempt);
        }

        UpdatedAt = attempt.AttemptedAt > UpdatedAt ? attempt.AttemptedAt : UpdatedAt;

        if (IsComplete())
        {
            return;
        }

        if (_attempts.Any(a => a.Status == NotificationAttemptStatus.Delivered))
        {
            Status = NotificationStatus.Delivered;
        }
        else if (_attempts.All(a => a.Status == NotificationAttemptStatus.Failed))
        {
            Status = NotificationStatus.Failed;
        }
        else
        {
            Status = NotificationStatus.Sent;
        }
    }

    /// <summary>
    /// Records that the patient asked to be called back.
    /// </summary>
    /// <param name="at">When the request arrived.</param>
    /// <param name="sourceReference">Reference to the source resource carrying the request.</param>
    /// <remarks>
    /// A call request is a next action for the owner, with its own deadline. It is deliberately
    /// not a status: the patient has not confirmed they know their result, so the track stays
    /// incomplete and the acknowledgement clock keeps running.
    /// </remarks>
    public void RecordCallRequest(DateTimeOffset at, string? sourceReference)
    {
        CallRequested = true;
        SourceReference = sourceReference ?? SourceReference;
        UpdatedAt = at;
    }

    /// <summary>
    /// Records that a member of staff has spoken to the patient on the phone, which answers any call request.
    /// </summary>
    /// <param name="at">When the call outcome was recorded.</param>
    /// <param name="sourceReference">Reference to the record of the call.</param>
    /// <remarks>
    /// A call request stays open until someone has called the patient back; once a call is recorded it is
    /// answered, so the owner is not chased for a call that already happened. What the patient said on the call
    /// is recorded separately, as an acknowledgement, teach-back or rating.
    /// </remarks>
    public void RecordCallMade(DateTimeOffset at, string? sourceReference)
    {
        CallRequested = false;
        SourceReference = sourceReference ?? SourceReference;
        UpdatedAt = at > UpdatedAt ? at : UpdatedAt;
    }

    /// <summary>
    /// Records how confident the patient said they feel about what to do next.
    /// </summary>
    /// <param name="confidence">The rating, from <see cref="MinimumConfidence"/> to <see cref="MaximumConfidence"/>.</param>
    /// <param name="at">When the rating arrived.</param>
    /// <param name="sourceReference">Reference to the source resource carrying the rating.</param>
    /// <remarks>
    /// A confidence rating is a measure, not a status: it changes neither <see cref="Status"/> nor
    /// whether the track is complete, because feeling confident is not the same as having
    /// acknowledged the result. A later rating replaces an earlier one, so the latest view stands.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="confidence"/> is outside the allowed range.</exception>
    public void RateConfidence(int confidence, DateTimeOffset at, string? sourceReference)
    {
        if (confidence is < MinimumConfidence or > MaximumConfidence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidence), confidence, $"A confidence rating must be between {MinimumConfidence} and {MaximumConfidence}.");
        }

        Confidence = confidence;
        SourceReference = sourceReference ?? SourceReference;
        UpdatedAt = at > UpdatedAt ? at : UpdatedAt;
    }

    /// <summary>
    /// Moves the track to a new status reported by the source.
    /// </summary>
    /// <param name="status">The status reported by the source system.</param>
    /// <param name="at">When the change happened according to the source.</param>
    /// <param name="sourceReference">Reference to the source resource carrying the status.</param>
    /// <remarks>Reporting the current status again is accepted and only refreshes the reference and time, so a resend is harmless.</remarks>
    /// <exception cref="InvalidOperationException">The change is not a valid move from the current status.</exception>
    public void Advance(NotificationStatus status, DateTimeOffset at, string? sourceReference)
    {
        if (status != Status && !AllowedMoves[Status].Contains(status))
        {
            throw new InvalidOperationException(
                $"The notification track cannot move from {Status} to {status}.");
        }

        Status = status;
        SourceReference = sourceReference ?? SourceReference;
        UpdatedAt = at;
    }

    /// <summary>
    /// Tells whether the track has reached its completing status.
    /// </summary>
    /// <returns><see langword="true"/> only when the patient has acknowledged or confirmed understanding.</returns>
    public bool IsComplete() =>
        Status is NotificationStatus.Acknowledged or NotificationStatus.Understood;
}
