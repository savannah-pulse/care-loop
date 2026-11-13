namespace CareLoop.Tests.Domain;

/// <summary>
/// Stage transitions are guarded: who may perform them and what they need.
/// </summary>
public sealed class TransitionRulesTests
{
    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Screening_result_moves_a_new_case_to_review_pending()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Review_moves_review_pending_to_follow_up_active()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Outcome_moves_follow_up_active_to_closure_review()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Event_that_does_not_leave_the_current_stage_is_refused()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Actor_with_a_role_not_allowed_for_the_transition_is_refused()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Track_events_during_follow_up_do_not_change_the_stage()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Any_event_on_a_closed_case_is_refused()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Stage_rules_come_from_the_pathway_definition_not_from_code()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Evaluate_never_mutates_the_case()
    {
    }
}
