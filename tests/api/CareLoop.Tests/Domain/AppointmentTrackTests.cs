using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// Scheduling is not attendance.
/// </summary>
public sealed class AppointmentTrackTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void Scheduled_appointment_is_not_complete()
    {
        var track = new AppointmentTrack("SYN-APPT-0001", _clock.UtcNow.AddDays(2), _clock.UtcNow);

        Assert.Equal(AppointmentStatus.Scheduled, track.Status);
        Assert.Equal(_clock.UtcNow.AddDays(2), track.ScheduledFor);
        Assert.False(track.IsComplete());
    }

    [Fact]
    public void Attended_appointment_without_outcome_is_not_complete()
    {
        var track = new AppointmentTrack("SYN-APPT-0001", _clock.UtcNow.AddDays(2), _clock.UtcNow);

        track.Advance(AppointmentStatus.Attended, _clock.UtcNow.AddDays(2), "SYN-APPT-0001");

        Assert.Equal(AppointmentStatus.Attended, track.Status);
        Assert.False(track.IsComplete());
    }

    [Fact]
    public void Attended_appointment_with_a_documented_outcome_is_complete()
    {
        var track = new AppointmentTrack("SYN-APPT-0001", _clock.UtcNow.AddDays(2), _clock.UtcNow);
        track.RecordOutcome("SYN-PROC-0001", _clock.UtcNow.AddDays(2));
        Assert.False(track.IsComplete());

        track.Advance(AppointmentStatus.Attended, _clock.UtcNow.AddDays(2), null);

        Assert.True(track.IsComplete());
    }

    [Fact]
    public void Missed_appointment_is_not_complete()
    {
        var track = new AppointmentTrack("SYN-APPT-0001", _clock.UtcNow.AddDays(2), _clock.UtcNow);

        track.Advance(AppointmentStatus.Missed, _clock.UtcNow.AddDays(2), null);

        Assert.Equal(AppointmentStatus.Missed, track.Status);
        Assert.False(track.IsComplete());
    }

    [Fact]
    public void Missed_appointment_can_be_rebooked()
    {
        var track = new AppointmentTrack("SYN-APPT-0001", _clock.UtcNow.AddDays(2), _clock.UtcNow);
        track.Advance(AppointmentStatus.Missed, _clock.UtcNow.AddDays(2), null);

        track.Rebook(_clock.UtcNow.AddDays(5), _clock.UtcNow.AddDays(3), "SYN-APPT-0002");

        Assert.Equal(AppointmentStatus.Scheduled, track.Status);
        Assert.Equal(_clock.UtcNow.AddDays(5), track.ScheduledFor);
        Assert.Equal("SYN-APPT-0002", track.SourceReference);
    }

    [Fact]
    public void Invalid_status_move_throws()
    {
        var track = new AppointmentTrack("SYN-APPT-0001", _clock.UtcNow.AddDays(2), _clock.UtcNow);
        track.Advance(AppointmentStatus.Attended, _clock.UtcNow, null);

        Assert.Throws<InvalidOperationException>(() => track.Advance(AppointmentStatus.Scheduled, _clock.UtcNow, null));
        Assert.Throws<InvalidOperationException>(() => track.Advance(AppointmentStatus.Missed, _clock.UtcNow, null));
    }
}
