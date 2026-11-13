namespace HisSimulator.State;

/// <summary>
/// Progress of one scenario step within a run.
/// </summary>
public enum ScenarioStepStatus
{
    /// <summary>Not attempted yet.</summary>
    Pending,

    /// <summary>The step's FHIR resources were sent and CareLoop answered with a success status.</summary>
    Sent,

    /// <summary>
    /// CareLoop answered with a client-error status. This is an expected result for some scenarios
    /// (for example a closure attempt without evidence), so it does not fail the run.
    /// </summary>
    Rejected,

    /// <summary>The step belongs to an actor other than the HIS and was left to that simulator. Unused while the HIS is the only actor.</summary>
    Skipped,

    /// <summary>The step could not be sent (transport error or server error).</summary>
    Failed,
}
