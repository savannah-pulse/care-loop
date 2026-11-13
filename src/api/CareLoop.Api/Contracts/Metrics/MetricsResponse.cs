namespace CareLoop.Contracts.Metrics;

/// <summary>
/// Loop-closure metrics returned by <c>GET /metrics</c>.
/// </summary>
/// <remarks>
/// These are coordination measures (how many cases have an owner, are on time, are closed with
/// evidence), not clinical outcomes.
/// </remarks>
public sealed record MetricsResponse
{
    /// <summary>When the metrics were computed.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Total number of cases.</summary>
    public required int TotalCases { get; init; }

    /// <summary>Number of cases not yet closed.</summary>
    public required int OpenCases { get; init; }

    /// <summary>Number of cases closed with evidence or a documented exception.</summary>
    public required int ClosedCases { get; init; }

    /// <summary>Number of open cases per stage name.</summary>
    public required IReadOnlyDictionary<string, int> CasesByStage { get; init; }

    /// <summary>Number of open cases per unresolved exception name.</summary>
    public required IReadOnlyDictionary<string, int> CasesByException { get; init; }

    /// <summary>Number of open cases past their deadline.</summary>
    public required int OverdueCases { get; init; }

    /// <summary>Number of open cases with an open escalation.</summary>
    public required int EscalatedCases { get; init; }

    /// <summary>Number of open cases in the facility lead queue with no named owner.</summary>
    public required int UnassignedCases { get; init; }

    /// <summary>Share of notifications the patient acknowledged, from 0 to 1; <see langword="null"/> when none were sent.</summary>
    public double? AcknowledgementRate { get; init; }

    /// <summary>Share of cases closed, from 0 to 1; <see langword="null"/> when there are no cases.</summary>
    public double? ClosureRate { get; init; }

    /// <summary>Median time from result received to closure; <see langword="null"/> when no case is closed.</summary>
    public TimeSpan? MedianTimeToClosure { get; init; }
}
