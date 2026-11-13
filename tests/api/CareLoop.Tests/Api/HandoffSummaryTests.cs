namespace CareLoop.Tests.Api;

/// <summary>
/// AI output is a draft; approval is a human event; a draft that cannot be traced to its facts is refused.
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
    public void Draft_request_contains_no_patient_name_or_contact_detail()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Draft_request_carries_only_finalized_facts_and_names_the_rest_as_gaps()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Draft_citing_an_unknown_fact_id_is_refused_and_nothing_is_stored()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Provider_timeout_or_refusal_returns_503_and_no_fallback_text()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Request_over_the_configured_size_limit_is_refused_before_any_call()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Instruction_like_text_in_a_fact_does_not_change_the_draft_or_the_case()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Draft_records_the_deployment_prompt_version_and_trigger()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Handoff_proposed_drafts_once_when_auto_draft_is_on_and_never_when_it_is_off()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Auto_draft_failure_does_not_fail_the_handoff()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Approval_records_a_HandoffSummaryApproved_event_naming_the_approver()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Approval_stores_the_text_the_approver_submitted_not_the_drafted_text()
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

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Rejection_records_the_reviewer_and_reason_and_writes_no_case_event()
    {
    }

    // TODO(WS-9): implement this test.
    [Fact(Skip = "WS9: not implemented")]
    public void Approving_a_rejected_summary_returns_409()
    {
    }
}
