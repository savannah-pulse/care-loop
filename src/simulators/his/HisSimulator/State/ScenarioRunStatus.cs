namespace HisSimulator.State;

/// <summary>
/// Lifecycle of one scenario run inside the HIS simulator.
/// </summary>
public enum ScenarioRunStatus
{
    /// <summary>Accepted but no step has been attempted yet.</summary>
    Pending,

    /// <summary>At least one step has been attempted and more remain.</summary>
    Running,

    /// <summary>Every HIS step was attempted without a fatal error; individual steps may have been refused.</summary>
    Completed,

    /// <summary>A step could not be sent and the run stopped.</summary>
    Failed,
}
