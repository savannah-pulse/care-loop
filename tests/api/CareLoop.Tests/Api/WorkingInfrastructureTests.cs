using CareLoop.Api.Features.Summaries;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Notifications;
using CareLoop.Api.Infrastructure.Time;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Recipients;

namespace CareLoop.Tests.Api;

/// <summary>
/// The pieces that must work in the scaffold: the system clock, the no-op AI provider and the
/// not-yet-built SMS and email providers refusing to send.
/// </summary>
public sealed class WorkingInfrastructureTests
{
    [Fact]
    public void System_clock_reports_utc()
    {
        var now = new SystemClock().UtcNow;

        Assert.Equal(TimeSpan.Zero, now.Offset);
    }

    [Fact]
    public async Task No_op_ai_provider_is_disabled_and_drafts_nothing()
    {
        var provider = new NoOpAiProvider();
        var request = new HandoffSummaryRequest
        {
            CaseId = Guid.NewGuid(),
            PathwayId = "breast-screening",
            Stage = CaseStage.FollowUpActive,
            CurrentOwnerRole = "clinician",
            Exceptions = [],
            TimelineLines = [],
            Language = "en",
        };

        Assert.False(provider.IsEnabled);
        Assert.Null(await provider.DraftHandoffSummaryAsync(request, CancellationToken.None));
    }

    [Fact]
    public void Sms_provider_is_a_stub_that_never_carries_sensitive_content()
    {
        var provider = new SmsNotificationProvider();
        var recipient = new Recipient { Reference = "SYN-PT-0001", Kind = RecipientKind.Patient, Language = "en", ContactPoints = [] };

        Assert.Equal(NotificationChannel.Sms, provider.Channel);
        Assert.False(provider.AllowsSensitiveContent);
        Assert.Throws<NotSupportedException>(() => provider.CanReach(recipient));
    }

    [Fact]
    public void Email_provider_is_a_stub()
    {
        var provider = new EmailNotificationProvider();
        var recipient = new Recipient { Reference = "SYN-PT-0001", Kind = RecipientKind.Patient, Language = "en", ContactPoints = [] };

        Assert.Equal(NotificationChannel.Email, provider.Channel);
        Assert.Throws<NotSupportedException>(() => provider.CanReach(recipient));
    }
}
