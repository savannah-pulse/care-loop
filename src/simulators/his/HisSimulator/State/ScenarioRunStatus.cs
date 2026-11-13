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

    /// <summary>Every step whose actor is <c>his</c> has been sent successfully.</summary>
    Completed,

    /// <summary>A step could not be sent and the run stopped.</summary>
    Failed,
}
