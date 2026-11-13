namespace CareLoop.Tests.Domain;

/// <summary>
/// A case always has exactly one accountable owner; transfer is propose-then-accept.
/// </summary>
public sealed class OwnershipRulesTests
{
    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void New_case_with_a_named_owner_is_owned_by_that_person()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void New_case_with_no_named_owner_goes_to_the_facility_lead_queue_and_is_flagged_unassigned()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Proposing_a_new_owner_does_not_change_the_accountable_owner()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Accepting_a_proposal_transfers_ownership()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Rejecting_a_proposal_returns_the_next_action_to_the_current_owner()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Rejected_handoff_raises_HandoffRejected_and_flags_the_exception()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Only_the_proposed_owner_can_accept_or_reject()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Second_proposal_while_one_is_pending_is_refused()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Proposal_on_a_closed_case_is_refused()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Proposal_carries_a_response_deadline_from_the_pathway()
    {
    }
}
