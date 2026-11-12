using CareLoop.Application.Intake;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Stages;

namespace CareLoop.Application.Stages.S02_CaseCreated;

/// <summary>Stage 2: the result was validated and deduplicated by intake, so the case is formally created.</summary>
public sealed class CaseCreatedStep : IAutomaticStageStep
{
    public CaseStage TargetStage => CaseStage.CaseCreated;

    public Task<bool> TryAdvanceAsync(CaseEventContext context, CoordinationCase @case, CancellationToken cancellationToken)
    {
        var result = context.AdvanceAutomatically(@case, TargetStage);
        return Task.FromResult(result.IsSuccess);
    }
}
