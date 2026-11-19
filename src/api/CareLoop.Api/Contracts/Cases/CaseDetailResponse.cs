using CareLoop.Contracts.Common;
using CareLoop.Domain.Cases;

namespace CareLoop.Contracts.Cases;

/// <summary>
/// The full coordination state of one case, returned by <c>GET /cases/{caseId}</c>.
/// </summary>
public sealed record CaseDetailResponse
{
    /// <summary>CareLoop's identifier of the case.</summary>
    public required Guid CaseId { get; init; }

    /// <summary>Identifier of the system that produced the result.</summary>
    public required string SourceSystem { get; init; }

    /// <summary>The source system's identifier of the actionable result. <see langword="null"/> for the coordinator, who sees no one's identity.</summary>
    public string? SourceResultId { get; init; }

    /// <summary>Source-system reference of the patient. Never a name. <see langword="null"/> for the coordinator, who sees no one's identity.</summary>
    public string? PatientReference { get; init; }

    /// <summary>Patient display name for staff; absent for a coordinator.</summary>
    public string? PatientDisplayName { get; init; }

    /// <summary>Patient's second identifier for staff; absent for a coordinator.</summary>
    public string? PatientSecondaryIdentifier { get; init; }

    /// <summary>The display step derived from stage and tracks by the Domain.</summary>
    public required CaseDisplayLabel DisplayLabel { get; init; }

    /// <summary>The escalation history, including acknowledgements and resolutions.</summary>
    public required IReadOnlyList<EscalationResponse> Escalations { get; init; }

    /// <summary>The latest outcome's evidence kind.</summary>
    public string? OutcomeEvidenceKind { get; init; }

    /// <summary>The patient's confidence rating, 1 to 5, when supplied.</summary>
    public int? Confidence { get; init; }

    /// <summary>Identifier of the pathway the case follows.</summary>
    public required string PathwayId { get; init; }

    /// <summary>The facility that produced the result.</summary>
    public required string FacilityId { get; init; }

    /// <summary>The pathway stage the case is in.</summary>
    public required CaseStage Stage { get; init; }

    /// <summary>The clinician-reviewed urgency code; <see langword="null"/> until the review arrives.</summary>
    public string? Urgency { get; init; }

    /// <summary>The one owner accountable for the next action.</summary>
    public required OwnerResponse Owner { get; init; }

    /// <summary>The owner a transfer is proposed to, while a proposal is pending. The current owner stays accountable until it is accepted.</summary>
    public OwnerResponse? ProposedOwner { get; init; }

    /// <summary>Key of the single next action.</summary>
    public string? NextAction { get; init; }

    /// <summary>When the next action is due.</summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary><see langword="true"/> while the case has an open escalation.</summary>
    public required bool IsEscalated { get; init; }

    /// <summary>Names of the unresolved exceptions on the case.</summary>
    public required IReadOnlyList<string> Exceptions { get; init; }

    /// <summary>The state of each parallel track.</summary>
    public required IReadOnlyList<TrackStatusResponse> Tracks { get; init; }

    /// <summary>Reference to the recorded outcome evidence, when there is any. <see langword="null"/> for the coordinator, who sees no one's identity.</summary>
    public string? OutcomeEvidenceReference { get; init; }

    /// <summary>The reason the case was closed; <see langword="null"/> while it is open.</summary>
    public ClosureReason? ClosureReason { get; init; }

    /// <summary>When the case was opened.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the case last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
