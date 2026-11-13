using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;

namespace CareLoop.Contracts.Events.Payloads;

/// <summary>
/// Payload of <see cref="CaseEventType.ClosureDecided"/>: an authorized person decided to close the case. The authorizer is the envelope&apos;s actor, taken from <c>Provenance</c>.
/// </summary>
public sealed record ClosurePayload : EventPayload
{
    /// <summary>The typed reason for closure.</summary>
    public required ClosureReason Reason { get; init; }

    /// <summary>Reference to the <c>Provenance</c> naming the authorizer.</summary>
    public required string ProvenanceReference { get; init; }

    /// <summary>Reference to the outcome evidence, when the reason requires it.</summary>
    public string? EvidenceReference { get; init; }

    /// <summary>The documented explanation, when closing on an exception.</summary>
    public string? ExceptionNote { get; init; }

    /// <summary>The next surveillance date, when the patient is on surveillance.</summary>
    public DateOnly? NextSurveillanceDate { get; init; }

    /// <summary>References to the documented contact attempts, when the patient is untraceable.</summary>
    public IReadOnlyList<string> DocumentedAttemptReferences { get; init; } = [];
}
