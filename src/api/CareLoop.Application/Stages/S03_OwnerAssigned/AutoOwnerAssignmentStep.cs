using CareLoop.Application.Intake;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Stages;

namespace CareLoop.Application.Stages.S03_OwnerAssigned;

/// <summary>
/// Stage 3 (automatic path): put the case in the accountable queue. With no approved routing, the case stays at
/// stage 2 and an OwnerUnassigned exception makes the gap visible until a person assigns an owner.
/// </summary>
public sealed class AutoOwnerAssignmentStep(IOwnerAssignmentPolicy policy) : IAutomaticStageStep
{
    public CaseStage TargetStage => CaseStage.OwnerAssigned;

    public Task<bool> TryAdvanceAsync(CaseEventContext context, CoordinationCase @case, CancellationToken cancellationToken)
    {
        var owner = policy.ResolveInitialOwner(@case);
        if (owner is null)
        {
            @case.RaiseException(
                ExceptionType.OwnerUnassigned,
                "No accountable owner or queue is configured; a coordinator must assign one.",
                context.PrimaryEventId, context.Now);
            return Task.FromResult(false);
        }

        var result = context.AdvanceAutomatically(@case, TargetStage);
        if (result.IsSuccess)
        {
            @case.AssignOwner(owner, context.Now);
        }

        return Task.FromResult(result.IsSuccess);
    }
}
