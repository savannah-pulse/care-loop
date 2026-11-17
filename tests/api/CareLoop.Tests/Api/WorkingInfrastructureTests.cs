using CareLoop.Api.Features.Summaries;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Time;

namespace CareLoop.Tests.Api;

/// <summary>
/// The pieces that must work in the scaffold: the system clock and the no-op AI provider. The text-message
/// and email providers are built; <c>SimulatorNotificationProviderTests</c> covers them.
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
            RequestId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            PathwayId = "breast-screening",
            Purpose = SummaryPurpose.ClinicianHandoff,
            Trigger = DraftTrigger.Requested,
            Facts = [],
            KnownGaps = [],
            Language = "en",
            PolicyVersion = "breast-screening-v1",
            PromptVersion = "fr11-v1",
        };

        Assert.False(provider.IsEnabled);
        Assert.Null(await provider.DraftHandoffSummaryAsync(request, CancellationToken.None));
    }
}
