using CareLoop.Domain.Events;

namespace CareLoop.Domain.Cases;

/// <summary>
/// One escalation raised on a case because a deadline passed without the required action.
/// </summary>
/// <remarks>
/// Escalation brings in oversight (facility lead, then coordinator). It does not move the case
/// to another stage and does not transfer ownership. The role it was addressed to may acknowledge
/// it (<see cref="AcknowledgedBy"/>, <see cref="AcknowledgedAt"/>), which records that the
/// oversight role has seen it and nothing more: acknowledging never clears an exception, changes
/// the owner or stops the clock, because only completing the overdue action resolves an escalation.
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

    /// <summary>
    /// The person who acknowledged the escalation, acting in the role it was escalated to;
    /// <see langword="null"/> until someone does.
    /// </summary>
    /// <remarks>Recorded for accountability only: it says who in the oversight role has seen the escalation. It does not make that person the owner.</remarks>
    public Actor? AcknowledgedBy { get; init; }

    /// <summary>When the escalation was acknowledged; <see langword="null"/> until it is.</summary>
    public DateTimeOffset? AcknowledgedAt { get; init; }

    /// <summary>When the overdue action was finally completed; <see langword="null"/> while the escalation is open.</summary>
    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary><see langword="true"/> while the overdue action has not been completed.</summary>
    public bool IsOpen => ResolvedAt is null;

    /// <summary><see langword="true"/> once the escalated-to role has acknowledged the escalation.</summary>
    public bool IsAcknowledged => AcknowledgedAt is not null;
}
