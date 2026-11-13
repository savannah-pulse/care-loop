namespace CareLoop.Domain.Tracks;

/// <summary>
/// The track for the follow-up appointment.
/// </summary>
/// <remarks>
/// <para>
/// <b>Definition.</b> The appointment at which follow-up care happens. Statuses:
/// <see cref="AppointmentStatus.Scheduled"/>, <see cref="AppointmentStatus.Attended"/>,
/// <see cref="AppointmentStatus.Missed"/>.
/// </para>
/// <para>
/// <b>Rules.</b> The track completes only when the patient attended and an outcome is documented.
/// A missed appointment creates a next action with an owner and a deadline, and the appointment
/// can then be rebooked.
/// </para>
/// <para>
/// <b>Why.</b> Scheduling is not attendance. Counting a booked appointment as done is one of the
/// ways a patient is lost to follow-up.
/// </para>
/// </remarks>
public sealed class AppointmentTrack
{
    /// <summary>
    /// Which statuses may follow which. A status not listed as a key is final.
    /// </summary>
    private static readonly IReadOnlyDictionary<AppointmentStatus, AppointmentStatus[]> AllowedMoves =
        new Dictionary<AppointmentStatus, AppointmentStatus[]>
        {
            [AppointmentStatus.Scheduled] = [AppointmentStatus.Attended, AppointmentStatus.Missed],
            [AppointmentStatus.Attended] = [],
            [AppointmentStatus.Missed] = [AppointmentStatus.Scheduled],
        };

    /// <summary>
    /// Starts the track when an appointment is booked. Used by <see cref="Cases.Case.BeginAppointment"/>.
    /// </summary>
    /// <param name="sourceReference">Reference to the <c>Appointment</c>.</param>
    /// <param name="scheduledFor">When the appointment is scheduled for.</param>
    /// <param name="startedAt">When the appointment was booked.</param>
    public AppointmentTrack(string? sourceReference, DateTimeOffset scheduledFor, DateTimeOffset startedAt)
    {
        Status = AppointmentStatus.Scheduled;
        SourceReference = sourceReference;
        ScheduledFor = scheduledFor;
        StartedAt = startedAt;
        UpdatedAt = startedAt;
    }

    /// <summary>
    /// Used only by the persistence layer when materializing a stored track.
    /// </summary>
    private AppointmentTrack()
    {
    }

    /// <summary>The current status of the track.</summary>
    public AppointmentStatus Status { get; private set; }

    /// <summary>Source-system reference of the resource the track follows.</summary>
    public string? SourceReference { get; private set; }

    /// <summary>When the track was started.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>When the status last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>When the appointment is or was scheduled for.</summary>
    public DateTimeOffset? ScheduledFor { get; private set; }

    /// <summary>Reference to the documented outcome of the appointment, or <see langword="null"/> until the case records outcome evidence.</summary>
    public string? OutcomeReference { get; private set; }

    /// <summary>
    /// Moves the track to a new status reported by the source.
    /// </summary>
    /// <param name="status">The status reported by the source system.</param>
    /// <param name="at">When the change happened according to the source.</param>
    /// <param name="sourceReference">Reference to the source resource carrying the status.</param>
    /// <remarks>Reporting the current status again is accepted and only refreshes the reference and time, so a resend is harmless. Use <see cref="Rebook"/> when a missed appointment is booked again, so the new date is recorded.</remarks>
    /// <exception cref="InvalidOperationException">The change is not a valid move from the current status.</exception>
    public void Advance(AppointmentStatus status, DateTimeOffset at, string? sourceReference)
    {
        if (status != Status && !AllowedMoves[Status].Contains(status))
        {
            throw new InvalidOperationException(
                $"The appointment track cannot move from {Status} to {status}.");
        }

        Status = status;
        SourceReference = sourceReference ?? SourceReference;
        UpdatedAt = at;
    }

    /// <summary>
    /// Books the appointment again after it was missed.
    /// </summary>
    /// <param name="scheduledFor">The new appointment time.</param>
    /// <param name="at">When the rebooking happened according to the source.</param>
    /// <param name="sourceReference">Reference to the new or updated <c>Appointment</c>.</param>
    /// <remarks>A missed appointment never parks the case: rebooking is the next action, and this records that it happened.</remarks>
    /// <exception cref="InvalidOperationException">The appointment cannot be rebooked from its current status.</exception>
    public void Rebook(DateTimeOffset scheduledFor, DateTimeOffset at, string? sourceReference)
    {
        Advance(AppointmentStatus.Scheduled, at, sourceReference);
        ScheduledFor = scheduledFor;
    }

    /// <summary>
    /// Links the outcome evidence recorded on the case to this appointment.
    /// </summary>
    /// <param name="evidenceReference">Reference to the <c>Procedure</c> or follow-up <c>DiagnosticReport</c>.</param>
    /// <param name="at">When the outcome was recorded.</param>
    /// <remarks>
    /// Called by <see cref="Cases.Case.RecordOutcomeEvidence"/>. The outcome may be reported before
    /// or after the attendance, so the track accepts it in any status and
    /// <see cref="IsComplete"/> asks for both.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="evidenceReference"/> is empty.</exception>
    public void RecordOutcome(string evidenceReference, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceReference);

        OutcomeReference = evidenceReference;
        UpdatedAt = at;
    }

    /// <summary>
    /// Tells whether the track has reached its completing status.
    /// </summary>
    /// <returns><see langword="true"/> only when the appointment was attended and an outcome is documented on the case.</returns>
    public bool IsComplete() =>
        Status == AppointmentStatus.Attended && OutcomeReference is not null;
}
