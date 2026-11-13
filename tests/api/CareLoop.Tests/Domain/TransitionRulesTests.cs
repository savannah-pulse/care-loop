using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Rules;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// Stage transitions are guarded: who may perform them and what they need.
/// </summary>
public sealed class TransitionRulesTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void Screening_result_moves_a_new_case_to_review_pending()
    {
        var @case = TestData.NewCase(_clock.UtcNow);

        var result = TransitionRules.Evaluate(@case, CaseEventType.ScreeningResultReceived, DomainTestData.SystemActor(), DomainTestData.Pathway);

        Assert.True(result.Verdict.Allowed, result.Verdict.Reason);
        Assert.Equal(CaseStage.ReviewPending, result.NextStage);
        Assert.Equal("review-result", result.NextAction);
    }

    [Fact]
    public void Review_moves_review_pending_to_follow_up_active()
    {
        var @case = DomainTestData.InReviewPending(_clock);

        var result = TransitionRules.Evaluate(@case, CaseEventType.ClinicalReviewCompleted, DomainTestData.ClinicianActor(), DomainTestData.Pathway);

        Assert.True(result.Verdict.Allowed, result.Verdict.Reason);
        Assert.Equal(CaseStage.FollowUpActive, result.NextStage);
        Assert.Equal("acknowledge-notification", result.NextAction);
    }

    [Fact]
    public void Further_outcome_during_closure_review_is_allowed_without_leaving_the_stage()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);

        var result = TransitionRules.Evaluate(@case, CaseEventType.OutcomeRecorded, DomainTestData.ClinicianActor(), DomainTestData.Pathway);

        Assert.True(result.Verdict.Allowed, result.Verdict.Reason);
        Assert.Null(result.NextStage);
        Assert.Null(result.NextAction);
    }

    [Fact]
    public void Further_outcome_during_closure_review_still_needs_a_role_allowed_to_record_outcomes()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);

        Assert.False(TransitionRules.Evaluate(@case, CaseEventType.OutcomeRecorded, DomainTestData.ReceptionistActor(), DomainTestData.Pathway).Verdict.Allowed);
        Assert.False(TransitionRules.Evaluate(@case, CaseEventType.OutcomeRecorded, null, DomainTestData.Pathway).Verdict.Allowed);
        Assert.True(TransitionRules.Evaluate(@case, CaseEventType.OutcomeRecorded, new Actor { Reference = "SYN-LAB-0001", Role = "lab", FacilityId = "SYN-FAC-A" }, DomainTestData.Pathway).Verdict.Allowed);
    }

    [Fact]
    public void Outcome_before_follow_up_is_still_refused()
    {
        var @case = DomainTestData.InReviewPending(_clock);

        var result = TransitionRules.Evaluate(@case, CaseEventType.OutcomeRecorded, DomainTestData.ClinicianActor(), DomainTestData.Pathway);

        Assert.False(result.Verdict.Allowed);
    }

    [Fact]
    public void Outcome_moves_follow_up_active_to_closure_review()
    {
        var @case = DomainTestData.InFollowUp(_clock);

        var result = TransitionRules.Evaluate(@case, CaseEventType.OutcomeRecorded, DomainTestData.ClinicianActor(), DomainTestData.Pathway);

        Assert.True(result.Verdict.Allowed, result.Verdict.Reason);
        Assert.Equal(CaseStage.ClosureReview, result.NextStage);
        Assert.Equal("decide-closure", result.NextAction);
    }

    [Fact]
    public void Closure_moves_closure_review_to_closed()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);

        var result = TransitionRules.Evaluate(@case, CaseEventType.ClosureDecided, DomainTestData.ClinicianActor(), DomainTestData.Pathway);

        Assert.True(result.Verdict.Allowed, result.Verdict.Reason);
        Assert.Equal(CaseStage.Closed, result.NextStage);
        Assert.Null(result.NextAction);
    }

    [Fact]
    public void Event_that_does_not_leave_the_current_stage_is_refused()
    {
        var @case = DomainTestData.InFollowUp(_clock);

        var result = TransitionRules.Evaluate(@case, CaseEventType.ClosureDecided, DomainTestData.ClinicianActor(), DomainTestData.Pathway);

        Assert.False(result.Verdict.Allowed);
        Assert.Null(result.NextStage);
        Assert.Contains("ClosureDecided", result.Verdict.Reason);
    }

    [Fact]
    public void Actor_with_a_role_not_allowed_for_the_transition_is_refused()
    {
        var @case = DomainTestData.InReviewPending(_clock);

        var result = TransitionRules.Evaluate(@case, CaseEventType.ClinicalReviewCompleted, DomainTestData.ReceptionistActor(), DomainTestData.Pathway);

        Assert.False(result.Verdict.Allowed);
        Assert.Contains("receptionist", result.Verdict.Reason);
    }

    [Fact]
    public void Transition_without_an_actor_is_refused_when_the_stage_names_roles()
    {
        var @case = DomainTestData.InReviewPending(_clock);

        var result = TransitionRules.Evaluate(@case, CaseEventType.ClinicalReviewCompleted, actor: null, DomainTestData.Pathway);

        Assert.False(result.Verdict.Allowed);
    }

    [Fact]
    public void Track_events_during_follow_up_do_not_change_the_stage()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        var trackEvents = new[]
        {
            CaseEventType.NotificationUpdated,
            CaseEventType.ConsultationUpdated,
            CaseEventType.ReferralUpdated,
            CaseEventType.AppointmentUpdated,
        };

        foreach (var eventType in trackEvents)
        {
            var result = TransitionRules.Evaluate(@case, eventType, actor: null, DomainTestData.Pathway);

            Assert.True(result.Verdict.Allowed, $"{eventType}: {result.Verdict.Reason}");
            Assert.Null(result.NextStage);
            Assert.Null(result.NextAction);
        }
    }

    [Fact]
    public void Track_events_before_follow_up_are_refused()
    {
        var @case = DomainTestData.InReviewPending(_clock);

        var result = TransitionRules.Evaluate(@case, CaseEventType.AppointmentUpdated, DomainTestData.ClinicianActor(), DomainTestData.Pathway);

        Assert.False(result.Verdict.Allowed);
    }

    [Fact]
    public void Any_event_on_a_closed_case_is_refused()
    {
        var @case = DomainTestData.Closed(_clock);

        foreach (var eventType in Enum.GetValues<CaseEventType>())
        {
            var result = TransitionRules.Evaluate(@case, eventType, DomainTestData.ClinicianActor(), DomainTestData.Pathway);

            Assert.False(result.Verdict.Allowed, $"{eventType} was allowed on a closed case");
            Assert.Contains("closed", result.Verdict.Reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Stage_rules_come_from_the_pathway_definition_not_from_code()
    {
        var @case = DomainTestData.InReviewPending(_clock);
        var rewired = DomainTestData.Pathway with
        {
            Stages = DomainTestData.Pathway.Stages.Select(s => s.Stage == CaseStage.ReviewPending
                ? s with { LeavesOn = [CaseEventType.OutcomeRecorded], Next = CaseStage.ClosureReview, AllowedRoles = ["receptionist"] }
                : s).ToList(),
        };

        var original = TransitionRules.Evaluate(@case, CaseEventType.ClinicalReviewCompleted, DomainTestData.ClinicianActor(), rewired);
        var rewiredResult = TransitionRules.Evaluate(@case, CaseEventType.OutcomeRecorded, DomainTestData.ReceptionistActor(), rewired);

        Assert.False(original.Verdict.Allowed);
        Assert.True(rewiredResult.Verdict.Allowed, rewiredResult.Verdict.Reason);
        Assert.Equal(CaseStage.ClosureReview, rewiredResult.NextStage);
        Assert.Equal("decide-closure", rewiredResult.NextAction);
    }

    [Fact]
    public void Pathway_without_a_rule_for_the_current_stage_refuses_the_event()
    {
        var @case = DomainTestData.InReviewPending(_clock);
        var missing = DomainTestData.Pathway with
        {
            Stages = DomainTestData.Pathway.Stages.Where(s => s.Stage != CaseStage.ReviewPending).ToList(),
        };

        var result = TransitionRules.Evaluate(@case, CaseEventType.ClinicalReviewCompleted, DomainTestData.ClinicianActor(), missing);

        Assert.False(result.Verdict.Allowed);
    }

    [Fact]
    public void Evaluate_never_mutates_the_case()
    {
        var @case = DomainTestData.InReviewPending(_clock);
        var stage = @case.Stage;
        var owner = @case.Owner;
        var nextAction = @case.NextAction;
        var updatedAt = @case.UpdatedAt;
        var events = @case.DomainEvents.Count;

        TransitionRules.Evaluate(@case, CaseEventType.ClinicalReviewCompleted, DomainTestData.ClinicianActor(), DomainTestData.Pathway);
        TransitionRules.Evaluate(@case, CaseEventType.ClosureDecided, DomainTestData.ReceptionistActor(), DomainTestData.Pathway);

        Assert.Equal(stage, @case.Stage);
        Assert.Equal(owner, @case.Owner);
        Assert.Equal(nextAction, @case.NextAction);
        Assert.Equal(updatedAt, @case.UpdatedAt);
        Assert.Equal(events, @case.DomainEvents.Count);
    }
}
