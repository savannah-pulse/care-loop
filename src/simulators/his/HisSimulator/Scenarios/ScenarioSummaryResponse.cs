namespace HisSimulator.Scenarios;

/// <summary>
/// One scenario as listed by <c>GET /scenarios</c>.
/// </summary>
/// <param name="Name">Scenario key, used in <c>POST /scenarios/{name}/run</c>.</param>
/// <param name="Description">Human-readable description from the scenario file.</param>
/// <param name="PathwayId">Identifier of the pathway definition the scenario runs against.</param>
/// <param name="IsCohort">Whether the scenario is a cohort mix rather than a step list.</param>
/// <param name="HisStepCount">Number of steps this simulator would perform (actor <c>his</c>); zero for a cohort.</param>
/// <param name="TotalStepCount">Total number of steps in the file across all actors; zero for a cohort.</param>
/// <param name="Expect">The expected end state declared by the scenario, when present.</param>
public sealed record ScenarioSummaryResponse(
    string Name,
    string Description,
    string PathwayId,
    bool IsCohort,
    int HisStepCount,
    int TotalStepCount,
    ScenarioExpectation? Expect)
{
    /// <summary>
    /// Always <see langword="true"/>: scenarios only ever describe simulated events.
    /// </summary>
    public bool Synthetic => true;
}
