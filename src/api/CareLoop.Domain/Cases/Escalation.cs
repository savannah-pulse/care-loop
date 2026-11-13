namespace CareLoop.Domain.Cases;

/// <summary>
/// One escalation raised on a case because a deadline passed without the required action.
/// </summary>
/// <remarks>
/// Escalation brings in oversight (facility lead, then coordinator). It does not move the case
/// to another stage and does not transfer ownership.
/// </remarks>
public sealed record Escalation
{
    /// <summary>The step on the pathway's escalation ladder (1 is the first step).</summary>
    public required int Level { get; init; }

    /// <summary>The role or queue the escalation was addressed to.</summary>
    public required CaseOwner EscalatedTo { get; init; }

    /// <summary>When the escalation was raised.</summary>
    public required DateTimeOffset RaisedAt { get; init; }

    /// <summary>Why it was raised, for example which deadline passed.</summary>
    public required string Reason { get; init; }

    /// <summary>When the overdue action was finally completed; <see langword="null"/> while the escalation is open.</summary>
    public DateTimeOffset? ResolvedAt { get; init; }
}
