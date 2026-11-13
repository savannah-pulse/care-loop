namespace CareLoop.Tests.Api;

/// <summary>
/// The deadline worker warns and escalates from the pathway definition.
/// </summary>
public sealed class DeadlineWorkerTests
{
    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Approaching_deadline_raises_DeadlineApproaching_once()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Passed_deadline_flags_the_case_overdue()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Passed_deadline_escalates_to_the_facility_lead_then_the_coordinator()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Sweep_twice_at_the_same_instant_changes_nothing_the_second_time()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Escalation_does_not_change_owner_stage_or_urgency()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Closed_cases_are_ignored()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Unacknowledged_notification_past_deadline_flags_unacknowledged()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Each_change_is_saved_as_a_DeadlineElapsed_event_and_audited()
    {
    }
}
