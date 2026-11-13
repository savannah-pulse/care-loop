using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;

namespace CareLoop.Contracts.Events.Payloads;

/// <summary>
/// Payload of <see cref="CaseEventType.OutcomeRecorded"/>: outcome evidence was recorded at the source.
/// </summary>
public sealed record OutcomePayload : EventPayload
{
    /// <summary>Reference to the <c>Procedure</c> or follow-up <c>DiagnosticReport</c>; becomes the evidence reference of the case.</summary>
    public required string EvidenceReference { get; init; }

    /// <summary>
    /// The kind of evidence, as a code the pathway definition declares in its evidence kinds (for
    /// example <c>diagnosis</c>, <c>treatment-plan</c>, <c>treatment-started</c>). An outcome with a
    /// kind the pathway does not declare is refused at intake and the attempt is audited.
    /// </summary>
    public required string EvidenceKind { get; init; }
}
