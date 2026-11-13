namespace CareLoop.Tests.Api;

/// <summary>
/// Each of the eight handlers applies its event through the Domain rules.
/// </summary>
public sealed class EventHandlerTests
{
    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Screening_result_with_no_named_owner_lands_in_the_facility_lead_queue()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Second_screening_result_for_the_same_result_is_refused()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Clinical_review_stores_urgency_and_requests_notification()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Review_with_an_unknown_urgency_code_is_refused()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Delivery_report_does_not_complete_the_notification_track()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void All_channels_failed_flags_unreachable_and_sets_a_next_action()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Acknowledgement_completes_the_notification_track()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Call_me_request_raises_CallRequested_for_the_current_owner()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Consultation_owner_proposed_keeps_the_previous_owner_accountable()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Consultation_rejected_returns_next_action_to_current_owner()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Referral_accepted_keeps_the_case_in_follow_up()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Referral_rejected_raises_ReferralRejected_and_sets_a_new_deadline()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Missed_appointment_creates_a_next_action_with_a_deadline()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Outcome_moves_the_case_to_closure_review()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Closure_with_evidence_closes_the_case()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Closure_without_evidence_is_rejected_and_audited()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Closure_without_an_authorizer_is_rejected_and_audited()
    {
    }
}
