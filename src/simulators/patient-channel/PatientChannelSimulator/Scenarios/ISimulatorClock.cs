namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// The simulator's only source of the current time.
/// </summary>
/// <remarks>
/// Nothing in the simulator reads the system time directly, so scenario replays and tests can
/// control time. This is the simulator's own abstraction: it shares no code with CareLoop.
/// </remarks>
public interface ISimulatorClock
{
    /// <summary>
    /// The current instant in UTC.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
