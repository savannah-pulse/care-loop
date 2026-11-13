namespace HisSimulator.Time;

/// <summary>
/// The simulator's only source of the current time.
/// </summary>
/// <remarks>
/// Nothing in the simulator reads the system time directly. Scenario replay paces its steps and
/// stamps each FHIR resource with the time it is sent, and tests need to control time, so every
/// timestamp must come through this seam. This is the simulator's own abstraction; it is
/// deliberately not shared with CareLoop's <c>IClock</c>.
/// </remarks>
public interface ISimulatorClock
{
    /// <summary>
    /// The current instant in UTC.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
