using CareLoop.Application.Abstractions;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Stages;

namespace CareLoop.Application.Intake;

/// <summary>
/// What a handler works with while processing one event. It enforces the ordering rules that keep the
/// event log coherent: the primary event is appended first, derived (orchestrator) events follow.
/// </summary>
public sealed class CaseEventContext
{
    private readonly ICaseEventStore events;

    internal CaseEventContext(
        ValidatedEnvelope envelope, DateTimeOffset receivedAt, ICaseRepository cases,
        ICaseEventStore events, CoordinationCase? resolvedCase)
    {
        Envelope = envelope;
        Now = receivedAt;
        Cases = cases;
        Case = resolvedCase;
        this.events = events;
    }

    public ValidatedEnvelope Envelope { get; }

    public EventActor Actor => Envelope.Actor;

    public DateTimeOffset Now { get; }

    public ICaseRepository Cases { get; }

    /// <summary>The case named by <c>caseId</c> in the envelope, if any.</summary>
    public CoordinationCase? Case { get; }

    public Guid PrimaryEventId { get; } = Guid.CreateVersion7();

    public CaseEvent? PrimaryEvent { get; private set; }

    /// <summary>Opens a new case at stage 1 and appends the primary event to it.</summary>
    public CoordinationCase OpenCase(string patientRef, string resultRef)
    {
        var @case = CoordinationCase.Open(Envelope.SourceSystem, patientRef, resultRef, PrimaryEventId, Actor, Now);
        Cases.Add(@case);
        AppendPrimaryEvent(@case);
        return @case;
    }

    public CaseEvent AppendPrimaryEvent(CoordinationCase @case, EventDisposition disposition = EventDisposition.Accepted)
    {
        if (PrimaryEvent is not null)
        {
            throw new InvalidOperationException("The primary event was already appended.");
        }

        PrimaryEvent = CaseEvent.Create(
            PrimaryEventId, @case.Id, @case.NextEventSequence(Now), Envelope.EventType, Envelope.SchemaVersion,
            Envelope.SourceSystem, Envelope.SourceEventId, Envelope.OccurredAt, Now,
            Envelope.PatientRef, Envelope.ResultRef, Actor, Envelope.PayloadJson, Envelope.ContentHash, disposition);
        events.Append(PrimaryEvent);
        return PrimaryEvent;
    }

    /// <summary>Advances a stage because of the primary event, attributing it to the submitting actor.</summary>
    public TransitionResult Advance(CoordinationCase @case, CaseStage target, TransitionEvidence? evidence = null)
    {
        EnsurePrimaryAppended();
        return @case.TryAdvance(target, PrimaryEventId, Actor, Now, evidence);
    }

    /// <summary>Advances a stage on behalf of the orchestrator, recording the stage-entry event.</summary>
    public TransitionResult AdvanceAutomatically(CoordinationCase @case, CaseStage target, string? payloadJson = null)
    {
        EnsurePrimaryAppended();
        var error = StageTransitionGuard.Evaluate(@case.Stage, target, EventActor.Orchestrator, null);
        if (error is not null)
        {
            return new TransitionResult(null, error);
        }

        var derived = DeriveEvent(@case, StageCatalog.Get(target).EntryEventType, payloadJson);
        return @case.TryAdvance(target, derived.Id, EventActor.Orchestrator, Now);
    }

    /// <summary>Appends an orchestrator-generated event; its source ID is derived from the primary event so replays are safe.</summary>
    public CaseEvent DeriveEvent(CoordinationCase @case, string eventType, string? payloadJson = null)
    {
        EnsurePrimaryAppended();
        var sourceEventId = $"{PrimaryEventId}/{eventType}";
        var derived = CaseEvent.Create(
            Guid.CreateVersion7(), @case.Id, @case.NextEventSequence(Now), eventType, EnvelopeSchema.CurrentVersion,
            EventActor.OrchestratorId, sourceEventId, Now, Now, @case.PatientRef, @case.ResultRef,
            EventActor.Orchestrator, payloadJson,
            ContentHasher.Hash(EventActor.OrchestratorId, sourceEventId, eventType, payloadJson),
            EventDisposition.Accepted);
        events.Append(derived);
        return derived;
    }

    private void EnsurePrimaryAppended()
    {
        if (PrimaryEvent is null)
        {
            throw new InvalidOperationException("Append the primary event before deriving events or advancing the case.");
        }
    }
}
