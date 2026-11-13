using CareLoop.Domain.Time;

namespace CareLoop.Tests.Fakes;

/// <summary>
/// A clock tests control: time stands still until a test moves it.
/// </summary>
public sealed class FakeClock : IClock
{
    /// <summary>Creates a clock set to a fixed, arbitrary instant.</summary>
    public FakeClock()
        : this(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero))
    {
    }

    /// <summary>Creates a clock set to <paramref name="start"/>.</summary>
    public FakeClock(DateTimeOffset start) => UtcNow = start.ToUniversalTime();

    /// <inheritdoc/>
    public DateTimeOffset UtcNow { get; private set; }

    /// <summary>Moves the clock forward by <paramref name="duration"/>.</summary>
    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "The fake clock cannot go backwards.");
        }

        UtcNow = UtcNow.Add(duration);
    }

    /// <summary>Sets the clock to <paramref name="instant"/>.</summary>
    public void Set(DateTimeOffset instant) => UtcNow = instant.ToUniversalTime();
}
