namespace MessagingSimulator.State;

/// <summary>
/// The state of one scenario run, as stored by the simulator and returned by the scenario routes.
/// </summary>
/// <remarks>
/// A run covers the patients of one scenario and collects every reaction to the notifications
/// those patients receive. The hospital-system events of the same scenario are replayed by the
/// HIS simulator independently.
/// </remarks>
/// <param name="RunId">Simulator-local identifier of the run, returned when the run is started.</param>
/// <param name="ScenarioName">Name of the scenario whose patient behaviours are in force (the file name without extension).</param>
/// <param name="Status">Where the run is in its lifecycle.</param>
/// <param name="StartedAt">When the behaviours were put in force.</param>
/// <param name="CompletedAt">When the run reached a final status; <see langword="null"/> while it is pending or running.</param>
/// <param name="PatientSyntheticIds">The patients whose behaviour this run set.</param>
/// <param name="Reactions">Every reaction so far to notifications for those patients, oldest first.</param>
/// <param name="Simulated">Visible simulated-data marker; always <see langword="true"/>.</param>
public sealed record ScenarioRunState(
    string RunId,
    string ScenarioName,
    ScenarioRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<string> PatientSyntheticIds,
    IReadOnlyList<PatientReactionRecord> Reactions,
    bool Simulated = true);
