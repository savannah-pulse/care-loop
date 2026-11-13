namespace HisSimulator.State;

/// <summary>
/// State of one execution of a scenario by the HIS simulator; also the body of <c>GET /scenarios/runs/{runId}</c>.
/// </summary>
/// <param name="RunId">Identifier of the run, returned when the run is started.</param>
/// <param name="ScenarioName">Name of the scenario being replayed.</param>
/// <param name="Status">Overall progress of the run.</param>
/// <param name="StartedAt">When the run started, in real time; each step is due at this instant plus its compressed offset.</param>
/// <param name="CompletedAt">When the run reached a final status, or <see langword="null"/> while it is in progress.</param>
/// <param name="Steps">One entry per step in the scenario, in file order.</param>
public sealed record ScenarioRun(
    Guid RunId,
    string ScenarioName,
    ScenarioRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<ScenarioStepResult> Steps)
{
    /// <summary>
    /// Always <see langword="true"/>: every record produced by a run is simulated.
    /// </summary>
    public bool Synthetic => true;
}
