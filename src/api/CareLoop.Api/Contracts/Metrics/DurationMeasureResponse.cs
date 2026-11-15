namespace CareLoop.Contracts.Metrics;

/// <summary>Median elapsed clock time and the number of cases that reached that step.</summary>
public sealed record DurationMeasureResponse
{
    /// <summary>Number of eligible cases that reached the measured step.</summary>
    public required int Denominator { get; init; }
    /// <summary>Median duration, or null when the step has not been reached.</summary>
    public TimeSpan? Median { get; init; }
}
