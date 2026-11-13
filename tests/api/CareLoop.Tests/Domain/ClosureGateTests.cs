namespace CareLoop.Tests.Domain;

/// <summary>
/// Closure needs an authorized human decision with evidence or a documented exception.
/// </summary>
public sealed class ClosureGateTests
{
    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Closure_with_evidence_by_an_authorized_role_is_allowed()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Closure_without_evidence_is_rejected()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Closure_by_an_unauthorized_role_is_rejected()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Closure_by_the_system_role_is_rejected()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Premature_closure_before_any_outcome_is_rejected()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Sent_referral_is_not_accepted_as_evidence_of_completed_care()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Accepted_referral_is_not_accepted_as_evidence_of_completed_care()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Surveillance_closure_without_a_next_date_is_rejected()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Declined_closure_without_an_exception_note_is_rejected()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Transferred_closure_requires_acceptance_elsewhere()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Untraceable_closure_without_documented_attempts_is_rejected()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Closing_an_already_closed_case_is_rejected()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Reason_without_a_rule_in_the_pathway_is_rejected()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Rejection_states_the_reason_for_the_audit_trail()
    {
    }
}
