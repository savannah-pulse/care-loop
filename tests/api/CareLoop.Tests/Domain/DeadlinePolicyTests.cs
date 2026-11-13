namespace CareLoop.Tests.Domain;

/// <summary>
/// Deadlines and escalation steps come only from the pathway definition.
/// </summary>
public sealed class DeadlinePolicyTests
{
    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Due_time_is_now_plus_the_pathway_duration()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Urgency_specific_deadline_is_preferred_over_the_general_one()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Unknown_next_action_throws()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Case_inside_the_warning_window_is_approaching()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Case_past_its_deadline_is_overdue()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Closed_case_is_never_overdue()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void First_escalation_step_goes_to_the_facility_lead()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Second_escalation_step_goes_to_the_coordinator()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void Each_escalation_step_is_raised_only_once_per_deadline()
    {
    }

    // TODO(WS-5): implement this test.
    [Fact(Skip = "WS5: not implemented")]
    public void No_duration_is_hard_coded_in_the_policy()
    {
    }
}
