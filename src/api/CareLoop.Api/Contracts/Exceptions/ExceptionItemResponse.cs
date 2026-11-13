using CareLoop.Contracts.Common;
using CareLoop.Domain.Cases;

namespace CareLoop.Contracts.Exceptions;

/// <summary>
/// One row in the exception queue returned by <c>GET /exceptions</c>.
/// </summary>
/// <remarks>
/// A case appears here only while it has an unresolved exception (overdue, rejected, unreachable,
/// unacknowledged or unassigned). The row shows the single next action and who is accountable for it.
/// </remarks>
public sealed record ExceptionItemResponse
{
    /// <summary>CareLoop's identifier of the case.</summary>
    public required Guid CaseId { get; init; }

    /// <summary>Source-system reference of the patient. Never a name.</summary>
    public required string PatientReference { get; init; }

    /// <summary>Names of the unresolved exceptions on the case.</summary>
    public required IReadOnlyList<string> Exceptions { get; init; }

    /// <summary>The pathway stage the case is in.</summary>
    public required CaseStage Stage { get; init; }

    /// <summary>The clinician-reviewed urgency code; <see langword="null"/> until the review arrives.</summary>
    public string? Urgency { get; init; }

    /// <summary>Key of the single next action.</summary>
    public required string NextAction { get; init; }

    /// <summary>The owner accountable for the next action.</summary>
    public required OwnerResponse Owner { get; init; }

    /// <summary>When the next action is due.</summary>
    public required DateTimeOffset DueAt { get; init; }

    /// <summary>How long the next action is overdue; <see langword="null"/> when it is not overdue.</summary>
    public TimeSpan? OverdueBy { get; init; }

    /// <summary><see langword="true"/> while the case has an open escalation.</summary>
    public required bool IsEscalated { get; init; }

    /// <summary>The highest escalation level reached; zero when never escalated.</summary>
    public required int EscalationLevel { get; init; }
}
