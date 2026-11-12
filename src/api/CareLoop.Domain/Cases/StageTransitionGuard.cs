using CareLoop.Domain.Stages;

namespace CareLoop.Domain.Cases;

public sealed record TransitionEvidence(
    string? Reason = null,
    IReadOnlyList<string>? EvidenceReferences = null,
    string? ExceptionAuthorisationReference = null);

public sealed record TransitionError(string Code, string Message);

/// <summary>Outcome of a guarded stage change.</summary>
public sealed record TransitionResult(StageTransition? Transition, TransitionError? Error)
{
    public bool IsSuccess => Transition is not null;

    public static TransitionResult Success(StageTransition transition) => new(transition, null);

    public static TransitionResult Failure(string code, string message) => new(null, new TransitionError(code, message));
}

/// <summary>
/// Pure rules for moving between stages. Every stage change must pass through here so the
/// ten-stage order and the closure gate cannot be bypassed by an individual event handler.
/// </summary>
public static class StageTransitionGuard
{
    public const string NotSequential = "stage_not_sequential";
    public const string ClosureRequiresHuman = "closure_requires_human_actor";
    public const string ClosureRequiresReason = "closure_requires_reason";
    public const string ClosureRequiresEvidence = "closure_requires_evidence";

    public static TransitionError? Evaluate(
        CaseStage current, CaseStage target, EventActor actor, TransitionEvidence? evidence)
    {
        if ((int)target != (int)current + 1)
        {
            return new TransitionError(NotSequential,
                $"Cannot move from {StageCatalog.Get(current).Code} to {StageCatalog.Get(target).Code}; " +
                "stages advance one at a time and never backwards.");
        }

        if (target == CaseStage.Closed)
        {
            return EvaluateClosure(actor, evidence);
        }

        return null;
    }

    private static TransitionError? EvaluateClosure(EventActor actor, TransitionEvidence? evidence)
    {
        if (actor.Kind != ActorKind.Human)
        {
            return new TransitionError(ClosureRequiresHuman, "Only an authorized human can close a case.");
        }

        if (string.IsNullOrWhiteSpace(evidence?.Reason))
        {
            return new TransitionError(ClosureRequiresReason, "Closure requires a typed reason.");
        }

        var hasEvidence = evidence.EvidenceReferences is { Count: > 0 };
        var hasException = !string.IsNullOrWhiteSpace(evidence.ExceptionAuthorisationReference);
        if (!hasEvidence && !hasException)
        {
            return new TransitionError(ClosureRequiresEvidence,
                "Closure requires evidence references or a documented, authorized exception.");
        }

        return null;
    }
}
