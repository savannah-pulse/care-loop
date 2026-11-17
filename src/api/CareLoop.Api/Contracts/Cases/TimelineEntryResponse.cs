using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;

namespace CareLoop.Contracts.Cases;

/// <summary>
/// One audit entry in a case timeline, returned by <c>GET /cases/{caseId}/timeline</c>.
/// </summary>
public sealed record TimelineEntryResponse
{
    /// <summary>Identifier of the audit entry.</summary>
    public required Guid AuditEventId { get; init; }

    /// <summary>The source system joined from the stored event; internal events use careloop.</summary>
    public required string SourceSystem { get; init; }

    /// <summary>The kind of event.</summary>
    public required CaseEventType EventType { get; init; }

    /// <summary>Whether the event was applied or refused (for example a closure attempt without evidence).</summary>
    public required SourceEventDisposition Disposition { get; init; }

    /// <summary>The source system or actor responsible.</summary>
    public required string ActorReference { get; init; }

    /// <summary>The role the actor acted in.</summary>
    public required string ActorRole { get; init; }

    /// <summary>When the action happened according to the source.</summary>
    public required DateTimeOffset SourceTimestamp { get; init; }

    /// <summary>When CareLoop received it.</summary>
    public required DateTimeOffset ReceivedTimestamp { get; init; }

    /// <summary>The stage before the event; <see langword="null"/> when the event opened the case.</summary>
    public CaseStage? PriorStage { get; init; }

    /// <summary>The stage after the event.</summary>
    public required CaseStage NewStage { get; init; }

    /// <summary>Why the change was made or refused.</summary>
    public required string Reason { get; init; }

    /// <summary>Reference to the supporting evidence, when there is any.</summary>
    public string? EvidenceReference { get; init; }

    /// <summary>
    /// Where the entry came from: <c>hospital</c> (the case's source system), <c>careloop</c> (CareLoop itself, including
    /// a call a nurse recorded), <c>sms</c> or <c>email</c> (a delivery report or the patient's reply on that channel).
    /// </summary>
    public required string Origin { get; init; }
}
