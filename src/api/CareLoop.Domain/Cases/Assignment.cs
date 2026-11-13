using CareLoop.Domain.Events;

namespace CareLoop.Domain.Cases;

/// <summary>
/// One step in the two-step ownership transfer of a case: a proposal and its response.
/// </summary>
/// <remarks>
/// Ownership transfer is propose-then-accept so that responsibility can never be dropped between
/// two people. Until <see cref="Status"/> is <see cref="AssignmentStatus.Accepted"/> the previous
/// owner stays accountable.
/// </remarks>
public sealed record Assignment
{
    /// <summary>The owner the case is proposed to.</summary>
    public required CaseOwner ProposedOwner { get; init; }

    /// <summary>Who proposed the transfer.</summary>
    public required Actor ProposedBy { get; init; }

    /// <summary>When the transfer was proposed.</summary>
    public required DateTimeOffset ProposedAt { get; init; }

    /// <summary>When the proposed owner must have responded, from the pathway definition.</summary>
    public required DateTimeOffset RespondBy { get; init; }

    /// <summary>Whether the proposal is pending, accepted or rejected.</summary>
    public required AssignmentStatus Status { get; init; }

    /// <summary>When the proposed owner responded; <see langword="null"/> while pending.</summary>
    public DateTimeOffset? RespondedAt { get; init; }

    /// <summary>The reason given when the proposal was rejected.</summary>
    public string? RejectionReason { get; init; }
}
