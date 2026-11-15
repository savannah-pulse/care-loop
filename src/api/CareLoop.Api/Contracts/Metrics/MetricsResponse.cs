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

    /// <summary>Rates keyed by measure: acknowledgement, closure, understanding, nextStepOnTime, referralAcceptedAndAttended, appointmentsAttended, treatmentStartedOnTime and signedEvidence.</summary>
    public required IReadOnlyDictionary<string, RateMeasureResponse> Measures { get; init; }

    /// <summary>Elapsed clock-time medians keyed by acknowledgement, consultation, appointment, careStarted and closure.</summary>
    public required IReadOnlyDictionary<string, DurationMeasureResponse> TimeToStep { get; init; }

    /// <summary>Closed cases per authorized typed closure reason.</summary>
    public required IReadOnlyDictionary<string, int> ClosuresByReason { get; init; }

    /// <summary>Cases that actually reached each display label in an applied audit projection; no skipped labels are inferred.</summary>
    public required IReadOnlyDictionary<string, RateMeasureResponse> CasesReachingDisplayLabel { get; init; }

    /// <summary>Current cases per Domain display label.</summary>
    public required IReadOnlyDictionary<string, int> CasesByDisplayLabel { get; init; }

    /// <summary>Share of notified cases that supplied a confidence rating; null when none were notified.</summary>
    public double? ConfidenceRate { get; init; }

    /// <summary>Number of notified cases eligible to supply a confidence rating.</summary>
    public required int ConfidenceDenominator { get; init; }

    /// <summary>Number of cases supplying a rating.</summary>
    public required int ConfidenceResponses { get; init; }

    /// <summary>Mean latest rating, 1 to 5; null when no rating was supplied.</summary>
    public double? AverageConfidence { get; init; }
}
