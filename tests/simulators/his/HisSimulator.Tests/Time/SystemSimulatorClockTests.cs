using HisSimulator.Time;

namespace HisSimulator.Tests.Time;

/// <summary>Behaviour of <see cref="SystemSimulatorClock"/>, which is implemented in the scaffold.</summary>
public sealed class SystemSimulatorClockTests
{
    [Fact]
    public void UtcNow_is_in_utc_and_does_not_go_backwards()
    {
        var clock = new SystemSimulatorClock();

        var first = clock.UtcNow;
        var second = clock.UtcNow;

        Assert.Equal(TimeSpan.Zero, first.Offset);
        Assert.True(second >= first);
    }
}
