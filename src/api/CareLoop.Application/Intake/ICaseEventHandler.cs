using CareLoop.Domain.Stages;
using CareLoop.Domain.Cases;

namespace CareLoop.Application.Intake;

/// <summary>
/// One handler per event type. Add a class implementing this interface and it is picked up automatically;
/// there is no central registry to edit. Handlers must change state only through the case aggregate and
/// <see cref="CaseEventContext"/>, never by writing to storage directly.
/// </summary>
public interface ICaseEventHandler
{
    string EventType { get; }

    ActorPermission Permission { get; }

    Task<HandlerResult> HandleAsync(CaseEventContext context, CancellationToken cancellationToken);
}

/// <summary>
/// A stage the orchestrator can enter without an external trigger. After each accepted event the runner
/// tries the step for the next stage until one declines, so the ten-stage flow stays sequential.
/// </summary>
public interface IAutomaticStageStep
{
    CaseStage TargetStage { get; }

    /// <summary>Returns true when the case advanced to <see cref="TargetStage"/>.</summary>
    Task<bool> TryAdvanceAsync(CaseEventContext context, CoordinationCase @case, CancellationToken cancellationToken);
}

public sealed class AutomaticStageRunner
{
    private readonly Dictionary<CaseStage, IAutomaticStageStep> steps;

    public AutomaticStageRunner(IEnumerable<IAutomaticStageStep> steps)
    {
        this.steps = steps
            .GroupBy(s => s.TargetStage)
            .ToDictionary(g => g.Key, g => g.Count() == 1
                ? g.Single()
                : throw new InvalidOperationException($"Multiple automatic steps target stage {g.Key}."));
    }

    public async Task RunAsync(CaseEventContext context, CoordinationCase @case, CancellationToken cancellationToken)
    {
        while (@case.Stage != StageCatalog.Last
            && steps.TryGetValue(@case.Stage + 1, out var step)
            && await step.TryAdvanceAsync(context, @case, cancellationToken))
        {
        }
    }
}
