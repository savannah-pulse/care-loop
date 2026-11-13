using CareLoop.Domain.Events;

namespace CareLoop.Domain.Cases;

/// <summary>
/// A person's decision to close a case, with the evidence or documented exception that supports it.
/// </summary>
/// <remarks>
/// Closure needs a human. A decision is only a request until <see cref="Rules.ClosureGate"/> has
/// allowed it; CareLoop never creates one itself and AI can never supply one.
/// </remarks>
public sealed record ClosureDecision
{
    /// <summary>The typed reason for closing.</summary>
    public required ClosureReason Reason { get; init; }

    /// <summary>The person who authorized the closure (from FHIR <c>Provenance</c>).</summary>
    public required Actor AuthorizedBy { get; init; }

    /// <summary>When the decision was made according to the source.</summary>
    public required DateTimeOffset DecidedAt { get; init; }

    /// <summary>Reference to the outcome evidence (for example a <c>Procedure</c>), when the reason requires evidence.</summary>
    public string? EvidenceReference { get; init; }

    /// <summary>The documented explanation when closing on an exception (declined, transferred, untraceable).</summary>
    public string? ExceptionNote { get; init; }

    /// <summary>The next surveillance date; required when <see cref="Reason"/> is <see cref="ClosureReason.OnSurveillance"/>.</summary>
    public DateOnly? NextSurveillanceDate { get; init; }

    /// <summary>References to the documented contact attempts; required when <see cref="Reason"/> is <see cref="ClosureReason.Untraceable"/>.</summary>
    public IReadOnlyList<string> DocumentedAttemptReferences { get; init; } = [];
}
