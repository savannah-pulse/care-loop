using CareLoop.Domain.Cases;

namespace CareLoop.Domain.Pathways;

/// <summary>
/// What a pathway requires before a case may be closed for a given reason.
/// </summary>
/// <remarks>
/// Closure is where CareLoop stops watching a patient, so what counts as enough evidence is a
/// clinical-governance decision and is configured per pathway rather than coded.
/// </remarks>
public sealed record ClosureEvidenceRule
{
    /// <summary>The closure reason the rule applies to.</summary>
    public required ClosureReason Reason { get; init; }

    /// <summary>The role keys allowed to authorize closure for this reason.</summary>
    public required IReadOnlyList<string> AuthorizedRoles { get; init; }

    /// <summary><see langword="true"/> when the reason is a documented exception rather than completed care.</summary>
    public required bool IsException { get; init; }

    /// <summary><see langword="true"/> when an outcome evidence reference must be present.</summary>
    public required bool RequiresEvidenceReference { get; init; }

    /// <summary><see langword="true"/> when a written exception note must be present.</summary>
    public required bool RequiresExceptionNote { get; init; }

    /// <summary><see langword="true"/> when a next surveillance date must be present.</summary>
    public required bool RequiresNextSurveillanceDate { get; init; }

    /// <summary>The minimum number of documented contact attempts required; zero when none are needed.</summary>
    public required int MinimumDocumentedAttempts { get; init; }

    /// <summary>The clinical or policy source the evidence requirement is taken from, or <see langword="null"/> while it is a placeholder with no source.</summary>
    public string? Citation { get; init; }
}
