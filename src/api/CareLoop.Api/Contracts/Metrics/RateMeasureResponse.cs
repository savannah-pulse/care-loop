namespace CareLoop.Contracts.Metrics;

/// <summary>A coordination rate with its explicit population, not a clinical effectiveness claim.</summary>
public sealed record RateMeasureResponse
{
    /// <summary>Cases satisfying the measure.</summary>
    public required int Numerator { get; init; }
    /// <summary>Cases eligible for the measure.</summary>
    public required int Denominator { get; init; }
    /// <summary>Numerator divided by denominator, or null when no cases are eligible.</summary>
    public double? Rate => Denominator == 0 ? null : (double)Numerator / Denominator;
}
