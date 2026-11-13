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
/// A missed appointment creates a next action with an owner and a deadline.
/// </para>
/// <para>
/// <b>Why.</b> Scheduling is not attendance. Counting a booked appointment as done is one of the
/// ways a patient is lost to follow-up.
/// </para>
/// </remarks>
public sealed class AppointmentTrack
{
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

    /// <summary>
    /// Moves the track to a new status reported by the source.
    /// </summary>
    /// <param name="status">The status reported by the source system.</param>
    /// <param name="at">When the change happened according to the source.</param>
    /// <param name="sourceReference">Reference to the source resource carrying the status.</param>
    /// <exception cref="InvalidOperationException">The change is not a valid move from the current status.</exception>
    public void Advance(AppointmentStatus status, DateTimeOffset at, string? sourceReference)
    {
        // TODO(WS-1): validate the status move, then update Status, SourceReference and UpdatedAt.
        throw new NotImplementedException("WS1: AppointmentTrack.Advance");
    }

    /// <summary>
    /// Tells whether the track has reached its completing status.
    /// </summary>
    /// <returns><see langword="true"/> only when the appointment was attended and an outcome is documented on the case.</returns>
    public bool IsComplete()
    {
        // TODO(WS-1): return whether Status is the completing status for this track.
        throw new NotImplementedException("WS1: AppointmentTrack.IsComplete");
    }
}
