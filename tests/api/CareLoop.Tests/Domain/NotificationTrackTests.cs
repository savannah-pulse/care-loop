using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// A notification is not an acknowledgement.
/// </summary>
public sealed class NotificationTrackTests
{
    private readonly FakeClock _clock = new();

    private NotificationAttempt Attempt(NotificationChannel channel, NotificationAttemptStatus status, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Channel = channel,
            AttemptedAt = _clock.UtcNow,
            Status = status,
            UsedSmsSafeTemplate = channel == NotificationChannel.Sms,
        };

    [Fact]
    public void Delivered_if_any_channel_delivers()
    {
        var track = new NotificationTrack(_clock.UtcNow);

        track.RecordAttempt(Attempt(NotificationChannel.Sms, NotificationAttemptStatus.Failed));
        track.RecordAttempt(Attempt(NotificationChannel.Sms, NotificationAttemptStatus.Delivered));

        Assert.Equal(NotificationStatus.Delivered, track.Status);
    }

    [Fact]
    public void Failed_only_if_all_channels_fail()
    {
        var track = new NotificationTrack(_clock.UtcNow);

        track.RecordAttempt(Attempt(NotificationChannel.Sms, NotificationAttemptStatus.Failed));
        Assert.Equal(NotificationStatus.Failed, track.Status);

        track.RecordAttempt(Attempt(NotificationChannel.Sms, NotificationAttemptStatus.Sent));
        Assert.Equal(NotificationStatus.Sent, track.Status);

        track.RecordAttempt(Attempt(NotificationChannel.Email, NotificationAttemptStatus.Failed));
        Assert.Equal(NotificationStatus.Sent, track.Status);
    }

    [Fact]
    public void Delivery_alone_does_not_complete_the_track()
    {
        var track = new NotificationTrack(_clock.UtcNow);

        track.RecordAttempt(Attempt(NotificationChannel.Sms, NotificationAttemptStatus.Delivered));
        track.Advance(NotificationStatus.Delivered, _clock.UtcNow, "SYN-COMM-0001");

        Assert.Equal(NotificationStatus.Delivered, track.Status);
        Assert.False(track.IsComplete());
    }

    [Fact]
    public void Acknowledgement_on_any_channel_completes_the_track()
    {
        var track = new NotificationTrack(_clock.UtcNow);
        track.RecordAttempt(Attempt(NotificationChannel.Sms, NotificationAttemptStatus.Failed));
        track.RecordAttempt(Attempt(NotificationChannel.Sms, NotificationAttemptStatus.Delivered));

        track.Advance(NotificationStatus.Acknowledged, _clock.UtcNow, "SYN-COMM-0002");

        Assert.True(track.IsComplete());
        Assert.Equal("SYN-COMM-0002", track.SourceReference);
    }

    [Fact]
    public void Teach_back_completes_the_track()
    {
        var track = new NotificationTrack(_clock.UtcNow);
        track.Advance(NotificationStatus.Delivered, _clock.UtcNow, null);

        track.Advance(NotificationStatus.Understood, _clock.UtcNow, "SYN-COMM-0003");

        Assert.Equal(NotificationStatus.Understood, track.Status);
        Assert.True(track.IsComplete());
    }

    [Fact]
    public void Call_me_request_is_not_an_acknowledgement()
    {
        var track = new NotificationTrack(_clock.UtcNow);
        track.Advance(NotificationStatus.Delivered, _clock.UtcNow, null);

        track.RecordCallRequest(_clock.UtcNow, "SYN-COMM-0004");

        Assert.True(track.CallRequested);
        Assert.Equal(NotificationStatus.Delivered, track.Status);
        Assert.False(track.IsComplete());
    }

    [Fact]
    public void A_call_made_answers_the_call_request_and_acknowledges_nothing()
    {
        var track = new NotificationTrack(_clock.UtcNow);
        track.Advance(NotificationStatus.Delivered, _clock.UtcNow, null);
        track.RecordCallRequest(_clock.UtcNow, "SYN-COMM-0004");

        track.RecordCallMade(_clock.UtcNow.AddMinutes(30), "call-outcome/SYN");

        Assert.False(track.CallRequested);
        Assert.Equal(NotificationStatus.Delivered, track.Status);
        Assert.False(track.IsComplete());
    }

    [Fact]
    public void Each_send_is_recorded_as_its_own_attempt()
    {
        var track = new NotificationTrack(_clock.UtcNow);
        var simulatorAttemptId = Guid.NewGuid();

        track.RecordAttempt(Attempt(NotificationChannel.Sms, NotificationAttemptStatus.Sent, simulatorAttemptId));
        track.RecordAttempt(Attempt(NotificationChannel.Sms, NotificationAttemptStatus.Sent));
        track.RecordAttempt(Attempt(NotificationChannel.Sms, NotificationAttemptStatus.Delivered, simulatorAttemptId));

        Assert.Equal(2, track.Attempts.Count);
        Assert.Equal(NotificationAttemptStatus.Delivered, track.Attempts.Single(a => a.Id == simulatorAttemptId).Status);
        Assert.Equal(NotificationStatus.Delivered, track.Status);
    }

    [Fact]
    public void Late_delivery_report_does_not_undo_an_acknowledgement()
    {
        var track = new NotificationTrack(_clock.UtcNow);
        track.Advance(NotificationStatus.Acknowledged, _clock.UtcNow, null);

        track.RecordAttempt(Attempt(NotificationChannel.Sms, NotificationAttemptStatus.Failed));

        Assert.Equal(NotificationStatus.Acknowledged, track.Status);
        Assert.True(track.IsComplete());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Confidence_rating_is_stored_without_changing_the_status(int rating)
    {
        var track = new NotificationTrack(_clock.UtcNow);
        track.Advance(NotificationStatus.Delivered, _clock.UtcNow, null);

        track.RateConfidence(rating, _clock.UtcNow, "SYN-COMM-0005");

        Assert.Equal(rating, track.Confidence);
        Assert.Equal(NotificationStatus.Delivered, track.Status);
        Assert.False(track.IsComplete(), "feeling confident is not an acknowledgement");
        Assert.Equal("SYN-COMM-0005", track.SourceReference);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Confidence_rating_outside_one_to_five_is_refused(int rating)
    {
        var track = new NotificationTrack(_clock.UtcNow);

        Assert.Throws<ArgumentOutOfRangeException>(() => track.RateConfidence(rating, _clock.UtcNow, null));
        Assert.Null(track.Confidence);
    }

    [Fact]
    public void Later_confidence_rating_replaces_the_earlier_one()
    {
        var track = new NotificationTrack(_clock.UtcNow);
        track.RateConfidence(2, _clock.UtcNow, null);

        track.RateConfidence(4, _clock.UtcNow, null);

        Assert.Equal(4, track.Confidence);
    }

    [Fact]
    public void Invalid_status_move_throws()
    {
        var track = new NotificationTrack(_clock.UtcNow);
        track.Advance(NotificationStatus.Understood, _clock.UtcNow, null);

        Assert.Throws<InvalidOperationException>(() => track.Advance(NotificationStatus.Sent, _clock.UtcNow, null));
        Assert.Throws<InvalidOperationException>(() => track.Advance(NotificationStatus.Failed, _clock.UtcNow, null));
    }
}
