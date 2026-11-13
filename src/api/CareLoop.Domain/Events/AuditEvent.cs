namespace CareLoop.Domain.Events;

/// <summary>
/// One append-only entry in the audit trail of a case.
/// </summary>
/// <remarks>
/// Accountability requires being able to show, for any moment, who did what, on whose authority
/// and what changed. Every accepted event, every coordination change and every refused attempt
/// (such as an unsupported closure) appends an audit event; none is ever updated or deleted.
/// </remarks>
public sealed record AuditEvent
{
    /// <summary>Identifier of the audit entry.</summary>
    public required Guid Id { get; init; }

    /// <summary>The case the entry belongs to.</summary>
    public required Guid CaseId { get; init; }

    /// <summary>The stored <see cref="SourceEvent"/> that caused the entry.</summary>
    public required Guid SourceEventId { get; init; }

    /// <summary>The kind of event that was audited.</summary>
    public required CaseEventType EventType { get; init; }

    /// <summary>Whether the event was applied or refused.</summary>
    public required SourceEventDisposition Disposition { get; init; }

    /// <summary>The source system or actor reference responsible for the change.</summary>
    public required string ActorReference { get; init; }

    /// <summary>The role the actor acted in; <c>system</c> for automated coordination changes.</summary>
    public required string ActorRole { get; init; }

    /// <summary>When the action happened according to the source.</summary>
    public required DateTimeOffset SourceTimestamp { get; init; }

    /// <summary>When CareLoop received it.</summary>
    public required DateTimeOffset ReceivedTimestamp { get; init; }

    /// <summary>JSON snapshot of the case projection before the event; <see langword="null"/> when the event opened the case.</summary>
    public string? PriorProjectionJson { get; init; }

    /// <summary>JSON snapshot of the case projection after the event. Equal to the prior projection when the event was refused.</summary>
    public required string NewProjectionJson { get; init; }

    /// <summary>Why the change was made or refused.</summary>
    public required string Reason { get; init; }

    /// <summary>Reference to the supporting evidence, when there is any.</summary>
    public string? EvidenceReference { get; init; }
}
