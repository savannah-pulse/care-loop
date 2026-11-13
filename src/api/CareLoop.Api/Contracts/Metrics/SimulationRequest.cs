namespace CareLoop.Contracts.Metrics;

/// <summary>
/// Parameters for the with-and-without-escalation comparison started by <c>POST /metrics/simulation</c>.
/// </summary>
public sealed record SimulationRequest
{
    /// <summary>Name of the cohort scenario to replay, for example <c>cohort</c>.</summary>
    public required string CohortScenario { get; init; }

    /// <summary>Random seed overriding the scenario's own, so a run can be repeated exactly; <see langword="null"/> uses the scenario's seed.</summary>
    public int? Seed { get; init; }
}
