using CareLoop.Domain.Cases;
using CareLoop.Domain.Stages;

namespace CareLoop.Api.Tests;

public sealed class StageTransitionGuardTests
{
    private static readonly EventActor Human = new(ActorKind.Human, "staff-1", ActorRoles.Clinician);
    private static readonly EventActor Orchestrator = EventActor.Orchestrator;

    [Fact]
    public void Catalog_defines_ten_sequential_stages()
    {
        Assert.Equal(10, StageCatalog.All.Count);
        Assert.Equal(Enumerable.Range(1, 10), StageCatalog.All.Select(s => s.Number));
        Assert.Equal(10, StageCatalog.All.Select(s => s.Code).Distinct().Count());
        Assert.Equal(10, StageCatalog.All.Select(s => s.EntryEventType).Distinct().Count());
    }

    [Fact]
    public void Each_stage_may_only_advance_to_the_next_stage()
    {
        for (var from = 1; from <= 10; from++)
        {
            for (var to = 1; to <= 10; to++)
            {
                var actor = to == (int)CaseStage.Closed ? Human : Orchestrator;
                var error = StageTransitionGuard.Evaluate((CaseStage)from, (CaseStage)to, actor, ClosureEvidence);

                if (to == from + 1)
                {
                    Assert.True(error is null, $"{from}->{to} should be allowed but was: {error?.Message}");
                }
                else
                {
                    Assert.Equal(StageTransitionGuard.NotSequential, error?.Code);
                }
            }
        }
    }

    [Theory]
    [InlineData(ActorKind.System, StageTransitionGuard.ClosureRequiresHuman)]
    [InlineData(ActorKind.SourceSystem, StageTransitionGuard.ClosureRequiresHuman)]
    public void Closure_requires_a_human_actor(ActorKind kind, string expectedCode)
    {
        var error = StageTransitionGuard.Evaluate(
            CaseStage.ClosureReview, CaseStage.Closed, new EventActor(kind, "x"), ClosureEvidence);

        Assert.Equal(expectedCode, error?.Code);
    }

    [Fact]
    public void Closure_requires_a_reason_and_evidence_or_an_authorized_exception()
    {
        Assert.Equal(StageTransitionGuard.ClosureRequiresReason,
            StageTransitionGuard.Evaluate(CaseStage.ClosureReview, CaseStage.Closed, Human, null)?.Code);

        Assert.Equal(StageTransitionGuard.ClosureRequiresEvidence,
            StageTransitionGuard.Evaluate(CaseStage.ClosureReview, CaseStage.Closed, Human,
                new TransitionEvidence(Reason: "care-completed"))?.Code);

        Assert.Null(StageTransitionGuard.Evaluate(CaseStage.ClosureReview, CaseStage.Closed, Human, ClosureEvidence));

        Assert.Null(StageTransitionGuard.Evaluate(CaseStage.ClosureReview, CaseStage.Closed, Human,
            new TransitionEvidence("patient-declined", ExceptionAuthorisationReference: "EXC-1")));
    }

    private static TransitionEvidence ClosureEvidence { get; } =
        new("care-completed", ["EVID-1"]);
}
