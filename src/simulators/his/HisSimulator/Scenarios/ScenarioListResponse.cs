namespace HisSimulator.Scenarios;

/// <summary>
/// Body of <c>GET /scenarios</c>.
/// </summary>
/// <param name="Scenarios">Every scenario found in the configured directory, ordered by name.</param>
public sealed record ScenarioListResponse(IReadOnlyList<ScenarioSummaryResponse> Scenarios)
{
    /// <summary>
    /// Always <see langword="true"/>: the list contains simulated scenarios only.
    /// </summary>
    public bool Synthetic => true;
}
