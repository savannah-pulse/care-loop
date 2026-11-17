namespace MessagingSimulator.Scenarios;

/// <summary>
/// <see cref="ISimulatorClock"/> backed by the machine's clock; the default outside tests.
/// </summary>
public sealed class SystemSimulatorClock : ISimulatorClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
