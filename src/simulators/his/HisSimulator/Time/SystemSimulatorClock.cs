namespace HisSimulator.Time;

/// <summary>
/// <see cref="ISimulatorClock"/> backed by the operating system clock; the default registration.
/// </summary>
public sealed class SystemSimulatorClock : ISimulatorClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
