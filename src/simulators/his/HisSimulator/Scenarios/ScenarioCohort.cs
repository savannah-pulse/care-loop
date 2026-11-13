namespace HisSimulator.Scenarios;

/// <summary>
/// A seeded mix of step-based scenarios replayed for many synthetic patients (<c>$defs/cohort</c> in the schema).
/// </summary>
public sealed record ScenarioCohort
{
    /// <summary>Number of synthetic cases to generate.</summary>
    public required int Size { get; init; }

    /// <summary>Random seed, so a cohort replays identically on every run and in both simulators.</summary>
    public required int Seed { get; init; }

    /// <summary>The scenarios to draw from and their relative weights.</summary>
    public required IReadOnlyList<ScenarioCohortMixEntry> Mix { get; init; }
}
