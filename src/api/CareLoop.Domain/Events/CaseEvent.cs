namespace CareLoop.Domain.Events;

public enum EventDisposition
{
    Accepted,

    /// <summary>Recorded for audit but not applied; a human must resolve the conflict.</summary>
    HeldForReview,
}

/// <summary>
/// Append-only coordination event. Together with <see cref="Cases.StageTransition"/> it lets the timeline
/// be reconstructed. Holds references only; clinical records stay in the source systems.
/// </summary>
public sealed class CaseEvent
{
    private CaseEvent() { }

    public Guid Id { get; private set; }
    public Guid CaseId { get; private set; }

    /// <summary>Per-case ordering; assigned by the case so the timeline is deterministic.</summary>
    public int CaseSequence { get; private set; }

    public string EventType { get; private set; } = "";
    public string SchemaVersion { get; private set; } = "";
    public string SourceSystem { get; private set; } = "";
    public string SourceEventId { get; private set; } = "";
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public string? PatientRef { get; private set; }
    public string? ResultRef { get; private set; }
    public Cases.ActorKind ActorKind { get; private set; }
    public string ActorId { get; private set; } = "";
    public string? ActorRole { get; private set; }
    public string? PayloadJson { get; private set; }

    /// <summary>Hash of the canonical envelope; separates a replay from a conflicting reuse of a source ID.</summary>
    public string ContentHash { get; private set; } = "";

    public EventDisposition Disposition { get; private set; }

    public static CaseEvent Create(
        Guid id, Guid caseId, int caseSequence, string eventType, string schemaVersion,
        string sourceSystem, string sourceEventId, DateTimeOffset occurredAt, DateTimeOffset receivedAt,
        string? patientRef, string? resultRef, Cases.EventActor actor, string? payloadJson,
        string contentHash, EventDisposition disposition) => new()
        {
            Id = id,
            CaseId = caseId,
            CaseSequence = caseSequence,
            EventType = eventType,
            SchemaVersion = schemaVersion,
            SourceSystem = sourceSystem,
            SourceEventId = sourceEventId,
            OccurredAt = occurredAt,
            ReceivedAt = receivedAt,
            PatientRef = patientRef,
            ResultRef = resultRef,
            ActorKind = actor.Kind,
            ActorId = actor.Id,
            ActorRole = actor.Role,
            PayloadJson = payloadJson,
            ContentHash = contentHash,
            Disposition = disposition,
        };
}
