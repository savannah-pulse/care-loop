using CareLoop.Contracts.Common;

namespace CareLoop.Contracts.Cases;

/// <summary>An oversight escalation; acknowledgement records seeing it, not resolving it.</summary>
public sealed record EscalationResponse
{
    /// <summary>The specific escalation a person may acknowledge.</summary>
    public required Guid EscalationId { get; init; }
    /// <summary>The pathway ladder step.</summary>
    public required int Level { get; init; }
    /// <summary>The oversight role and facility queue addressed.</summary>
    public required OwnerResponse EscalatedTo { get; init; }
    /// <summary>When oversight was requested.</summary>
    public required DateTimeOffset RaisedAt { get; init; }
    /// <summary>The overdue action that prompted oversight.</summary>
    public required string Reason { get; init; }
    /// <summary>Who acknowledged it, when anyone has.</summary>
    public string? AcknowledgedBy { get; init; }
    /// <summary>The role the acknowledger acted in.</summary>
    public string? AcknowledgedByRole { get; init; }
    /// <summary>When the oversight role saw it.</summary>
    public DateTimeOffset? AcknowledgedAt { get; init; }
    /// <summary>When the overdue work was resolved, independently of acknowledgement.</summary>
    public DateTimeOffset? ResolvedAt { get; init; }
}
