namespace CareLoop.Tests.Domain;

/// <summary>
/// Invariants of the Case aggregate.
/// </summary>
public sealed class CaseTests
{
    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Recording_a_review_stores_the_urgency_exactly_as_received()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Closed_case_cannot_move_to_another_stage()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Exception_always_leaves_a_next_action_an_owner_and_a_deadline()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Escalation_is_a_flag_and_does_not_change_stage_or_owner()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Resolving_escalations_clears_the_flag()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Recording_outcome_evidence_does_not_close_the_case()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Closing_raises_CaseClosed_and_clears_next_action()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Beginning_notification_raises_NotificationRequested()
    {
    }
}
