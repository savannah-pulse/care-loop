using CareLoop.Api.Infrastructure.Time;

namespace CareLoop.Tests.Api;

/// <summary>
/// The pieces that must work in the scaffold: the system clock. The text-message
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
}
