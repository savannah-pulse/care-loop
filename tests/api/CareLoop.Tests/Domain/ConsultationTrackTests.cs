using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// The consultation track completes only on a recorded outcome.
/// </summary>
public sealed class ConsultationTrackTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void Accepted_consultation_is_not_complete()
    {
        var track = new ConsultationTrack("SYN-SR-0001", _clock.UtcNow);

        track.Advance(ConsultationStatus.OwnerProposed, _clock.UtcNow, "SYN-TASK-0001");
        track.Advance(ConsultationStatus.Accepted, _clock.UtcNow, "SYN-TASK-0001");

        Assert.Equal(ConsultationStatus.Accepted, track.Status);
        Assert.False(track.IsComplete());
    }

    [Fact]
    public void Recorded_outcome_completes_the_track()
    {
        var track = new ConsultationTrack("SYN-SR-0001", _clock.UtcNow);
        track.Advance(ConsultationStatus.Accepted, _clock.UtcNow, null);

        track.Advance(ConsultationStatus.OutcomeRecorded, _clock.UtcNow, "SYN-TASK-0002");

        Assert.True(track.IsComplete());
        Assert.Equal("SYN-TASK-0002", track.SourceReference);
    }

    [Fact]
    public void Rejected_consultation_can_be_requested_again()
    {
        var track = new ConsultationTrack("SYN-SR-0001", _clock.UtcNow);
        track.Advance(ConsultationStatus.OwnerProposed, _clock.UtcNow, null);
        track.Advance(ConsultationStatus.Rejected, _clock.UtcNow, null);

        track.Advance(ConsultationStatus.Requested, _clock.UtcNow, "SYN-SR-0002");

        Assert.Equal(ConsultationStatus.Requested, track.Status);
        Assert.Equal("SYN-SR-0002", track.SourceReference);
        Assert.False(track.IsComplete());
    }

    [Fact]
    public void Invalid_status_move_throws()
    {
        var track = new ConsultationTrack("SYN-SR-0001", _clock.UtcNow);

        Assert.Throws<InvalidOperationException>(() => track.Advance(ConsultationStatus.OutcomeRecorded, _clock.UtcNow, null));

        track.Advance(ConsultationStatus.Accepted, _clock.UtcNow, null);
        track.Advance(ConsultationStatus.OutcomeRecorded, _clock.UtcNow, null);

        Assert.Throws<InvalidOperationException>(() => track.Advance(ConsultationStatus.Requested, _clock.UtcNow, null));
    }
}
