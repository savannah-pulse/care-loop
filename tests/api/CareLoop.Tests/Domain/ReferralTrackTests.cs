using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// A sent or accepted referral is never completed care.
/// </summary>
public sealed class ReferralTrackTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void Accepted_referral_completes_the_track()
    {
        var track = new ReferralTrack("SYN-REF-0001", _clock.UtcNow);

        track.Advance(ReferralStatus.Accepted, _clock.UtcNow, "SYN-TASK-0001");

        Assert.True(track.IsComplete());
        Assert.Equal("SYN-TASK-0001", track.SourceReference);
    }

    [Fact]
    public void Sent_referral_does_not_complete_the_track()
    {
        var track = new ReferralTrack("SYN-REF-0001", _clock.UtcNow);

        Assert.Equal(ReferralStatus.Sent, track.Status);
        Assert.False(track.IsComplete());
    }

    [Fact]
    public void Rejected_referral_records_the_reason()
    {
        var track = new ReferralTrack("SYN-REF-0001", _clock.UtcNow);

        track.Reject("PLACEHOLDER: service does not accept this referral type", _clock.UtcNow, "SYN-TASK-0002");

        Assert.Equal(ReferralStatus.Rejected, track.Status);
        Assert.Equal("PLACEHOLDER: service does not accept this referral type", track.RejectionReason);
        Assert.False(track.IsComplete());
    }

    [Fact]
    public void Re_sent_referral_clears_the_previous_rejection_reason()
    {
        var track = new ReferralTrack("SYN-REF-0001", _clock.UtcNow);
        track.Reject("PLACEHOLDER reason", _clock.UtcNow, null);

        track.Advance(ReferralStatus.Sent, _clock.UtcNow, "SYN-REF-0002");

        Assert.Null(track.RejectionReason);
        Assert.Equal("SYN-REF-0002", track.SourceReference);
    }

    [Fact]
    public void Invalid_status_move_throws()
    {
        var track = new ReferralTrack("SYN-REF-0001", _clock.UtcNow);
        track.Advance(ReferralStatus.Accepted, _clock.UtcNow, null);

        Assert.Throws<InvalidOperationException>(() => track.Advance(ReferralStatus.Sent, _clock.UtcNow, null));
        Assert.Throws<InvalidOperationException>(() => track.Advance(ReferralStatus.Rejected, _clock.UtcNow, null));
    }
}
