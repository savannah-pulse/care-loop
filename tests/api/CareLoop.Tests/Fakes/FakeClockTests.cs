namespace CareLoop.Tests.Fakes;

/// <summary>
/// The fake clock is working infrastructure, so it is tested for real.
/// </summary>
public sealed class FakeClockTests
{
    [Fact]
    public void Time_stands_still_until_advanced()
    {
        var clock = new FakeClock();
        var first = clock.UtcNow;

        Assert.Equal(first, clock.UtcNow);
    }

    [Fact]
    public void Advance_moves_time_forward()
    {
        var clock = new FakeClock();
        var start = clock.UtcNow;

        clock.Advance(TimeSpan.FromHours(48));

        Assert.Equal(start.AddHours(48), clock.UtcNow);
    }

    [Fact]
    public void Advance_backwards_is_refused()
    {
        var clock = new FakeClock();

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Set_converts_to_utc()
    {
        var clock = new FakeClock();

        clock.Set(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(4)));

        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
        Assert.Equal(8, clock.UtcNow.Hour);
    }
}
