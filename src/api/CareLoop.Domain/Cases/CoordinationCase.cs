using CareLoop.Domain.Stages;

namespace CareLoop.Domain.Cases;

/// <summary>
/// Case projection and consistency boundary. All state changes go through its methods;
/// <see cref="Version"/> is an optimistic concurrency token bumped by every change.
/// </summary>
public sealed class CoordinationCase
{
    private readonly List<CaseException> exceptions = [];
    private readonly List<StageTransition> transitions = [];

    private CoordinationCase() { }

    public Guid Id { get; private set; }
    public string SourceSystem { get; private set; } = "";
    public string PatientRef { get; private set; } = "";
    public string ResultRef { get; private set; } = "";
    public CaseStage Stage { get; private set; }
    public OwnerKind? OwnerKind { get; private set; }
    public string? OwnerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int LastEventSequence { get; private set; }
    public int Version { get; private set; }

    public IReadOnlyCollection<CaseException> Exceptions => exceptions;
    public IReadOnlyCollection<StageTransition> Transitions => transitions;

    public CaseOwner? Owner => OwnerKind is { } kind && OwnerId is { } id ? new CaseOwner(kind, id) : null;

    /// <summary>Opens a case at stage 1 and records the entry transition.</summary>
    public static CoordinationCase Open(
        string sourceSystem, string patientRef, string resultRef,
        Guid triggeringEventId, EventActor actor, DateTimeOffset at)
    {
        var @case = new CoordinationCase
        {
            Id = Guid.CreateVersion7(),
            SourceSystem = sourceSystem,
            PatientRef = patientRef,
            ResultRef = resultRef,
            Stage = StageCatalog.First,
            CreatedAt = at,
            UpdatedAt = at,
        };
        @case.transitions.Add(
            StageTransition.Create(@case.Id, null, StageCatalog.First, triggeringEventId, actor, at, null));
        return @case;
    }

    public int NextEventSequence(DateTimeOffset at)
    {
        Touch(at);
        return ++LastEventSequence;
    }

    /// <summary>Moves exactly one stage forward if <see cref="StageTransitionGuard"/> allows it.</summary>
    public TransitionResult TryAdvance(
        CaseStage target, Guid triggeringEventId, EventActor actor, DateTimeOffset at,
        TransitionEvidence? evidence = null)
    {
        var error = StageTransitionGuard.Evaluate(Stage, target, actor, evidence);
        if (error is not null)
        {
            return new TransitionResult(null, error);
        }

        var transition = StageTransition.Create(Id, Stage, target, triggeringEventId, actor, at, evidence);
        transitions.Add(transition);
        Stage = target;
        Touch(at);
        return TransitionResult.Success(transition);
    }

    public void AssignOwner(CaseOwner owner, DateTimeOffset at)
    {
        OwnerKind = owner.Kind;
        OwnerId = owner.Id;
        Touch(at);
    }

    /// <summary>Raises review work; a repeat for the same cause and event is ignored.</summary>
    public CaseException RaiseException(
        ExceptionType type, string reason, Guid eventId, DateTimeOffset at, CaseOwner? owner = null)
    {
        var existing = exceptions.FirstOrDefault(e =>
            e.Status == ExceptionStatus.Open && e.Type == type && e.RaisedByEventId == eventId);
        if (existing is not null)
        {
            return existing;
        }

        var exception = CaseException.Raise(Id, type, reason, eventId, at, owner);
        exceptions.Add(exception);
        Touch(at);
        return exception;
    }

    public bool HasOpenException(ExceptionType type) =>
        exceptions.Any(e => e.Status == ExceptionStatus.Open && e.Type == type);

    public void ResolveExceptions(ExceptionType type, Guid eventId, DateTimeOffset at)
    {
        foreach (var exception in exceptions.Where(e => e.Status == ExceptionStatus.Open && e.Type == type))
        {
            exception.Resolve(eventId, at);
            Touch(at);
        }
    }

    private void Touch(DateTimeOffset at)
    {
        UpdatedAt = at;
        Version++;
    }
}
