namespace CareLoop.Contracts.Metrics;

/// <summary>
/// The with-and-without-escalation comparison returned by <c>POST /metrics/simulation</c>.
/// </summary>
/// <remarks>
/// Both arms replay the same seeded synthetic cohort with a controlled clock. The comparison
/// illustrates the coordination effect only; it is not evidence of clinical benefit.
/// </remarks>
public sealed record SimulationResponse
{
    /// <summary>Name of the cohort scenario that was replayed.</summary>
    public required string CohortScenario { get; init; }

    /// <summary>The seed used, so the run can be repeated.</summary>
    public required int Seed { get; init; }

    /// <summary>The arm with deadlines and escalation active.</summary>
    public required SimulationArmResponse WithEscalation { get; init; }

    /// <summary>The arm with deadlines and escalation switched off.</summary>
    public required SimulationArmResponse WithoutEscalation { get; init; }
}
