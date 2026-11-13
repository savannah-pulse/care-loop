namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// A seeded cohort that mixes step-based scenarios (<c>$defs/cohort</c> in the schema).
/// </summary>
public sealed record ScenarioCohort
{
    /// <summary>Number of cases to generate.</summary>
    public required int Size { get; init; }

    /// <summary>Random seed so the cohort replays identically.</summary>
    public required int Seed { get; init; }

    /// <summary>The weighted scenarios the cohort draws from.</summary>
    public required IReadOnlyList<ScenarioCohortMixEntry> Mix { get; init; }
}
