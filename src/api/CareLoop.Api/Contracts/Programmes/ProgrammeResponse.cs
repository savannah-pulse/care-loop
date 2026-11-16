using CareLoop.Domain.Pathways;

namespace CareLoop.Contracts.Programmes;

/// <summary>The read-only, versioned pathway configuration; approvals happen outside CareLoop.</summary>
public sealed record ProgrammeResponse
{
    /// <summary>The pathway key.</summary>
    public required string PathwayId { get; init; }
    /// <summary>The current definition's version, not a historical case-version claim.</summary>
    public required string Version { get; init; }
    /// <summary>The configured programme name.</summary>
    public required string DisplayName { get; init; }
    /// <summary>Whether the definition still contains clinically unapproved placeholders.</summary>
    public required bool IsPlaceholder { get; init; }
    /// <summary>The reviewed urgency codes the pathway accepts.</summary>
    public required IReadOnlyList<string> UrgencyCodes { get; init; }
    /// <summary>The role accountable while a case has no named owner.</summary>
    public required string UnassignedQueueRole { get; init; }
    /// <summary>Configured stages and permitted roles.</summary>
    public required IReadOnlyList<StageDefinition> Stages { get; init; }
    /// <summary>Tracks required for closure and their exception next actions.</summary>
    public required IReadOnlyList<TrackDefinition> Tracks { get; init; }
    /// <summary>Deadlines with owners, citations and clinical approval status.</summary>
    public required IReadOnlyList<DeadlineDefinition> Deadlines { get; init; }
    /// <summary>Oversight steps and elapsed overdue thresholds.</summary>
    public required IReadOnlyList<EscalationStep> EscalationLadder { get; init; }
    /// <summary>Authorized closure reasons and evidence requirements.</summary>
    public required IReadOnlyList<ClosureEvidenceRule> ClosureRules { get; init; }
    /// <summary>Recognized outcome kinds and their next actions.</summary>
    public required IReadOnlyList<EvidenceKindDefinition> EvidenceKinds { get; init; }
    /// <summary>The configuration's documented change history.</summary>
    public required IReadOnlyList<PathwayChangeLogEntry> ChangeLog { get; init; }
}
