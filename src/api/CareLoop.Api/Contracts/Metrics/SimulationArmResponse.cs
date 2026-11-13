namespace CareLoop.Contracts.Metrics;

/// <summary>
/// The result of replaying the cohort under one condition (with or without escalation).
/// </summary>
public sealed record SimulationArmResponse
{
    /// <summary><see langword="true"/> when deadlines and escalation were active in this arm.</summary>
    public required bool EscalationEnabled { get; init; }

    /// <summary>Number of simulated cases replayed.</summary>
    public required int TotalCases { get; init; }

    /// <summary>Number of cases closed with evidence or a documented exception by the end of the replay.</summary>
    public required int ClosedCases { get; init; }

    /// <summary>Number of cases left without action past their deadline and never picked up.</summary>
    public required int StalledCases { get; init; }

    /// <summary>Number of escalations raised.</summary>
    public required int EscalationsRaised { get; init; }

    /// <summary>Median time from result received to closure; <see langword="null"/> when no case closed.</summary>
    public TimeSpan? MedianTimeToClosure { get; init; }
}
