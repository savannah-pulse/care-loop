using CareLoop.Domain.Stages;

namespace CareLoop.Domain.Cases;

/// <summary>Audit record of one stage change, linked to the event that caused it.</summary>
public sealed class StageTransition
{
    private StageTransition() { }

    public Guid Id { get; private set; }
    public Guid CaseId { get; private set; }
    public CaseStage? FromStage { get; private set; }
    public CaseStage ToStage { get; private set; }
    public Guid EventId { get; private set; }
    public ActorKind ActorKind { get; private set; }
    public string ActorId { get; private set; } = "";
    public string? ActorRole { get; private set; }
    public string? Reason { get; private set; }
    public List<string> EvidenceReferences { get; private set; } = [];
    public DateTimeOffset OccurredAt { get; private set; }

    internal static StageTransition Create(
        Guid caseId, CaseStage? from, CaseStage to, Guid eventId, EventActor actor,
        DateTimeOffset at, TransitionEvidence? evidence) => new()
        {
            Id = Guid.CreateVersion7(),
            CaseId = caseId,
            FromStage = from,
            ToStage = to,
            EventId = eventId,
            ActorKind = actor.Kind,
            ActorId = actor.Id,
            ActorRole = actor.Role,
            Reason = evidence?.Reason,
            EvidenceReferences = evidence?.EvidenceReferences?.ToList() ?? [],
            OccurredAt = at,
        };
}
