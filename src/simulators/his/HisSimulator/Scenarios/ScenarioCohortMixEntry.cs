namespace HisSimulator.Scenarios;

/// <summary>
/// One component of a cohort mix: a step-based scenario and its relative weight.
/// </summary>
public sealed record ScenarioCohortMixEntry
{
    /// <summary>Name of a step-based scenario in the same folder.</summary>
    public required string Scenario { get; init; }

    /// <summary>Relative weight (greater than zero) of this scenario within the cohort.</summary>
    public required double Weight { get; init; }
}
