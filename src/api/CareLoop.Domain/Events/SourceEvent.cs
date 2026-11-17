namespace CareLoop.Domain.Events;

/// <summary>
/// An immutable record of one thing CareLoop was told had happened to a case.
/// </summary>
/// <remarks>
/// <para>
/// <b>Definition.</b> A source event is an inbound fact: something that happened in a hospital
/// system or the patient channel (a result arrived, a referral was rejected, a patient
/// acknowledged), or one of the few events CareLoop raises internally (a deadline elapsed, a
/// handoff summary was approved). <see cref="CaseEventType"/> lists them. It is the opposite
/// direction to a <see cref="CareLoop.Domain.Events.DomainEvents.IDomainEvent"/>, which is
/// something CareLoop decides must be sent out.
/// </para>
/// <para>
/// <b>Rules.</b> Source events are append-only: they are the evidence of what CareLoop was told
/// and when. The pair <see cref="SourceSystem"/> plus <see cref="SourceEventId"/> is unique, which
/// is what makes ingestion idempotent when a hospital system resends the same state. An event the
/// rules refuse is still stored, marked as rejected, so the attempt is audited.
/// </para>
/// </remarks>
public sealed record SourceEvent
{
    /// <summary>CareLoop's identifier for this stored event.</summary>
    public required Guid Id { get; init; }

    /// <summary>Identifier of the system that produced the event. Internal events use <c>careloop</c>.</summary>
    public required string SourceSystem { get; init; }

    /// <summary>The source's identifier for the event: for FHIR, the resource identifier plus its version or last-updated time.</summary>
    public required string SourceEventId { get; init; }

    /// <summary>The kind of event; selects the handler that applies it.</summary>
    public required CaseEventType Type { get; init; }

    /// <summary>The case the event was applied to.</summary>
    public required Guid CaseId { get; init; }

    /// <summary>Who performed the action at the source, when the source reported it.</summary>
    public Actor? Actor { get; init; }

    /// <summary>When the action happened according to the source.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>When CareLoop received the event, taken from <see cref="Time.IClock"/>.</summary>
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>The event payload serialized as JSON by the API layer. Holds references and statuses, never clinical narrative.</summary>
    public required string PayloadJson { get; init; }

    /// <summary>Whether the event changed the case or was refused by a rule.</summary>
    public required SourceEventDisposition Disposition { get; init; }

    /// <summary>Why the event was refused, when <see cref="Disposition"/> is <see cref="SourceEventDisposition.Rejected"/>; otherwise an optional note.</summary>
    public string? DispositionReason { get; init; }

    /// <summary>Reference to the evidence the event carries (for example a <c>Procedure</c> reference), when it carries any.</summary>
    public string? EvidenceReference { get; init; }

    /// <summary>
    /// Who actually sent the event (<see cref="EventOrigins"/>) when it was not <see cref="SourceSystem"/> itself:
    /// CareLoop, or a text message or email channel. <see langword="null"/> when the source system sent it.
    /// </summary>
    public string? Origin { get; init; }
}
