using CareLoop.Contracts.Common;
using CareLoop.Domain.Cases;

namespace CareLoop.Contracts.Cases;

/// <summary>
/// One row in the case list returned by <c>GET /cases</c>.
/// </summary>
public sealed record CaseSummaryResponse
{
    /// <summary>CareLoop's identifier of the case.</summary>
    public required Guid CaseId { get; init; }

    /// <summary>Source-system reference of the patient. Never a name.</summary>
    public required string PatientReference { get; init; }

    /// <summary>Identifier of the pathway the case follows.</summary>
    public required string PathwayId { get; init; }

    /// <summary>The pathway stage the case is in.</summary>
    public required CaseStage Stage { get; init; }

    /// <summary>The clinician-reviewed urgency code; <see langword="null"/> until the review arrives.</summary>
    public string? Urgency { get; init; }

    /// <summary>The one owner accountable for the next action.</summary>
    public required OwnerResponse Owner { get; init; }

    /// <summary>Key of the single next action.</summary>
    public string? NextAction { get; init; }

    /// <summary>When the next action is due.</summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary><see langword="true"/> while the case has an open escalation.</summary>
    public required bool IsEscalated { get; init; }

    /// <summary>Names of the unresolved exceptions on the case; empty when there are none.</summary>
    public required IReadOnlyList<string> Exceptions { get; init; }

    /// <summary>When the case last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
