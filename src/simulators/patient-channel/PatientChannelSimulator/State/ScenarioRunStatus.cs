namespace PatientChannelSimulator.State;

/// <summary>
/// Lifecycle of one scenario run inside the simulator.
/// </summary>
/// <remarks>
/// A run does not send anything by itself. Starting one puts the scenario's patient behaviours
/// in force; from then on the simulator waits for CareLoop's notifications and reacts to them.
/// </remarks>
public enum ScenarioRunStatus
{
    /// <summary>The run was accepted and its patient behaviours are not in force yet.</summary>
    Pending,

    /// <summary>The patient behaviours are in force and the simulator is reacting to notifications as they arrive.</summary>
    Running,

    /// <summary>
    /// The run was stopped and every reaction so far was accepted by CareLoop. Not used yet: the
    /// simulator has no route that stops a run, so a run stays <see cref="Running"/> or becomes <see cref="Failed"/>.
    /// </summary>
    Completed,

    /// <summary>At least one reaction could not be sent or was rejected by CareLoop.</summary>
    Failed,
}
