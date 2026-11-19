using CareLoop.Api.Features.Notifications;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;
using static CareLoop.Tests.Api.Notifications.NotificationTestSupport;

namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// Redundant delivery: every enabled, reachable channel gets its own attempt.
/// </summary>
public sealed class NotificationSenderTests
{
    private static readonly Dictionary<string, string> Values = new() { ["caseReference"] = "AB12CD34", ["facility"] = "SYN Family Health Centre", ["facilityContact"] = "SYN-PHONE-A" };

    private readonly FakeClock _clock = new();
    private readonly FakeProvider _sms = new(NotificationChannel.Sms, allowsSensitiveContent: false);
    private readonly FakeProvider _email = new(NotificationChannel.Email);

    [Fact]
    public async Task Sends_on_all_enabled_reachable_channels()
    {
        var sender = Sender(_clock, Enabled("Sms", "Email"), _email, _sms);

        var attempts = await SendAsync(sender, Patient(NotificationChannel.Email, NotificationChannel.Sms));

        Assert.Equal([NotificationChannel.Sms, NotificationChannel.Email], attempts.Select(a => a.Channel));
        Assert.All(attempts, a => Assert.Equal(NotificationAttemptStatus.Sent, a.Status));
        Assert.Single(_sms.Sent);
        Assert.Single(_email.Sent);
        Assert.Equal("SYN-Sms-0001", _sms.Sent[0].ContactPoint.Address);
    }

    [Fact]
    public async Task Skips_a_provider_that_cannot_reach_the_recipient()
    {
        var sender = Sender(_clock, Enabled("Email", "Sms"), _email, _sms);

        var attempts = await SendAsync(sender, Patient(NotificationChannel.Email));

        Assert.Equal(NotificationChannel.Email, Assert.Single(attempts).Channel);
        Assert.Empty(_sms.Sent);
    }

    [Fact]
    public async Task Skips_a_provider_that_is_not_enabled()
    {
        var sender = Sender(_clock, Enabled("Email"), _email, _sms);

        var attempts = await SendAsync(sender, Patient(NotificationChannel.Email, NotificationChannel.Sms));

        Assert.Equal(NotificationChannel.Email, Assert.Single(attempts).Channel);
        Assert.Empty(_sms.Sent);
    }

    [Fact]
    public async Task Sms_gets_the_sms_template_and_email_the_standard_one_with_its_subject()
    {
        var sender = Sender(_clock, Enabled("Email", "Sms"), _email, _sms);

        var attempts = await SendAsync(sender, Patient(NotificationChannel.Email, NotificationChannel.Sms));

        var sms = Assert.Single(_sms.Sent);
        Assert.True(sms.IsSmsSafe);
        Assert.Null(sms.Subject);
        Assert.DoesNotContain('\n', sms.Body);
        Assert.False(Assert.Single(_email.Sent).IsSmsSafe);
        Assert.False(string.IsNullOrWhiteSpace(_email.Sent[0].Subject));
        Assert.Contains("AB12CD34", _email.Sent[0].Body);
        Assert.True(attempts.Single(a => a.Channel == NotificationChannel.Sms).UsedSmsSafeTemplate);
        Assert.False(attempts.Single(a => a.Channel == NotificationChannel.Email).UsedSmsSafeTemplate);
    }

    [Fact]
    public async Task Delivered_if_any_channel_delivers()
    {
        _sms.Answer = _ => new SendResult { Accepted = false, FailureReason = "gateway down" };
        var sender = Sender(_clock, Enabled("Email", "Sms"), _email, _sms);
        var attempts = await SendAsync(sender, Patient(NotificationChannel.Email, NotificationChannel.Sms));
        var track = new NotificationTrack(_clock.UtcNow);

        foreach (var attempt in attempts)
        {
            track.RecordAttempt(attempt);
        }

        track.RecordAttempt(attempts.Single(a => a.Channel == NotificationChannel.Email) with { Status = NotificationAttemptStatus.Delivered });
        Assert.Equal(NotificationStatus.Delivered, track.Status);
    }

    [Fact]
    public async Task Failed_only_if_all_channels_fail()
    {
        _sms.Answer = _ => new SendResult { Accepted = false, FailureReason = "gateway down" };
        var sender = Sender(_clock, Enabled("Email", "Sms"), _email, _sms);
        var attempts = await SendAsync(sender, Patient(NotificationChannel.Email, NotificationChannel.Sms));
        var track = new NotificationTrack(_clock.UtcNow);

        foreach (var attempt in attempts)
        {
            track.RecordAttempt(attempt);
        }

        Assert.Equal(NotificationStatus.Sent, track.Status);

        _email.Answer = _ => new SendResult { Accepted = false, FailureReason = "inbox full" };
        var allFailed = await SendAsync(sender, Patient(NotificationChannel.Email, NotificationChannel.Sms));
        var failedTrack = new NotificationTrack(_clock.UtcNow);
        foreach (var attempt in allFailed)
        {
            failedTrack.RecordAttempt(attempt);
        }

        Assert.All(allFailed, a => Assert.Equal(NotificationAttemptStatus.Failed, a.Status));
        Assert.Equal("inbox full", allFailed.Single(a => a.Channel == NotificationChannel.Email).FailureReason);
        Assert.Equal(NotificationStatus.Failed, failedTrack.Status);
    }

    [Fact]
    public async Task Each_send_is_recorded_as_its_own_attempt()
    {
        var sender = Sender(_clock, Enabled("Email", "Sms"), _email, _sms);
        var notificationId = Guid.NewGuid();

        var attempts = await SendAsync(sender, Patient(NotificationChannel.Email, NotificationChannel.Sms), notificationId);
        var again = await SendAsync(sender, Patient(NotificationChannel.Email, NotificationChannel.Sms), notificationId);

        Assert.Equal(2, attempts.Select(a => a.Id).Distinct().Count());
        Assert.Equal(attempts.Select(a => a.Id), again.Select(a => a.Id));
        Assert.Equal(attempts[0].Id, _sms.Sent[0].AttemptId);
        Assert.Equal(_clock.UtcNow, attempts[0].AttemptedAt);
        Assert.Equal($"fake-{attempts[0].Id:N}", attempts[0].ProviderMessageId);
    }

    [Fact]
    public async Task Throwing_provider_is_recorded_as_failed_and_does_not_stop_the_others()
    {
        _email.Answer = _ => throw new HttpRequestException("connection refused");
        var sender = Sender(_clock, Enabled("Email", "Sms"), _email, _sms);

        var attempts = await SendAsync(sender, Patient(NotificationChannel.Email, NotificationChannel.Sms));

        var failed = attempts.Single(a => a.Channel == NotificationChannel.Email);
        Assert.Equal(NotificationAttemptStatus.Failed, failed.Status);
        Assert.Contains("connection refused", failed.FailureReason);
        Assert.Equal(NotificationAttemptStatus.Sent, attempts.Single(a => a.Channel == NotificationChannel.Sms).Status);
        Assert.Single(_sms.Sent);
    }

    [Fact]
    public async Task Per_event_override_replaces_the_enabled_providers()
    {
        var options = Enabled("Email");
        options.PerEventOverrides["EscalationRaised"] = ["Sms"];
        var sender = Sender(_clock, options, _email, _sms);

        var escalation = await sender.SendAsync(Guid.NewGuid(), Patient(NotificationChannel.Email, NotificationChannel.Sms), "patient-follow-up", "EscalationRaised", Values, CancellationToken.None);
        var other = await sender.SendAsync(Guid.NewGuid(), Patient(NotificationChannel.Email, NotificationChannel.Sms), "patient-follow-up", "NotificationRequested", Values, CancellationToken.None);

        Assert.Equal(NotificationChannel.Sms, Assert.Single(escalation).Channel);
        Assert.Equal(NotificationChannel.Email, Assert.Single(other).Channel);
    }

    private static Task<IReadOnlyList<NotificationAttempt>> SendAsync(NotificationSender sender, Recipient recipient, Guid? notificationId = null) =>
        sender.SendAsync(Guid.NewGuid(), recipient, "patient-follow-up", "NotificationRequested", Values, CancellationToken.None, notificationId);
}
