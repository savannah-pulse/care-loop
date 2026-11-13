namespace CareLoop.Tests.Api;

/// <summary>
/// AI output is a draft; approval is a human event.
/// </summary>
public sealed class HandoffSummaryTests
{
    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Draft_with_the_no_op_provider_returns_409()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Draft_is_stored_as_draft_and_never_published()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Draft_request_contains_no_patient_detail()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Approval_records_a_HandoffSummaryApproved_event_naming_the_approver()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Approval_by_a_role_not_allowed_is_forbidden()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Approval_does_not_change_urgency_stage_or_owner()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Approving_twice_returns_409()
    {
    }
}
