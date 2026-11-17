namespace MessagingSimulator.Scenarios;

/// <summary>
/// One scenario as listed by <c>GET /scenarios</c>.
/// </summary>
/// <param name="Name">Scenario key, used in <c>POST /scenarios/{name}/run</c>.</param>
/// <param name="Description">The scenario's own description.</param>
/// <param name="PathwayId">Identifier of the pathway definition the scenario runs against.</param>
/// <param name="IsCohort">Whether the scenario is a cohort rather than a single journey.</param>
/// <param name="PatientCount">How many patient behaviours a run of this scenario puts in force.</param>
/// <param name="Synthetic">Visible simulated-data marker; always <see langword="true"/>.</param>
/// <param name="Problem">Why the scenario file cannot be used, or <see langword="null"/> when it can. A scenario with a problem cannot be run.</param>
public sealed record ScenarioSummary(
    string Name,
    string Description,
    string PathwayId,
    bool IsCohort,
    int PatientCount,
    bool Synthetic = true,
    string? Problem = null);
