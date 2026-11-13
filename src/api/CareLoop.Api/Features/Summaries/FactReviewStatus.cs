namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// Whether a fact supplied to an AI provider has been finalized by a person.
/// </summary>
/// <remarks>
/// A draft built on unfinalized facts would present a working note as a settled one, which is the
/// failure mode a handoff summary exists to prevent. Only finalized facts are sent; the other
/// values exist so the collector can refuse them explicitly rather than silently dropping them.
/// </remarks>
public enum FactReviewStatus
{
    /// <summary>The source has not finished with it. Never sent to a provider.</summary>
    Unfinalized = 0,

    /// <summary>A person finalized it; it may be summarised.</summary>
    Finalized = 1,

    /// <summary>Two sources disagree. Never sent as a fact; reported as a conflict for a person to resolve.</summary>
    Conflicting = 2,
}
