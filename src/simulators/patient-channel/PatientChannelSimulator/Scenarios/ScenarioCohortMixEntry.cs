namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// One weighted scenario in a cohort mix.
/// </summary>
public sealed record ScenarioCohortMixEntry
{
    /// <summary>Name of a step-based scenario in the same folder.</summary>
    public required string Scenario { get; init; }

    /// <summary>Relative weight of the scenario in the mix; an illustrative placeholder, not an observed rate.</summary>
    public required double Weight { get; init; }
}
