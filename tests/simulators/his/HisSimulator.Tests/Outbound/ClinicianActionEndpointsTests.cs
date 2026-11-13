namespace HisSimulator.Tests.Outbound;

/// <summary>Planned behaviour of the manual clinician action routes (<c>GET /cases</c>, <c>POST /actions/...</c>).</summary>
public sealed class ClinicianActionEndpointsTests
{
    [Fact(Skip = "WS7: not implemented")]
    public void GetCases_lists_simulated_cases()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void PostClinicalReview_sends_review_task_with_given_urgency()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void PostReferralDecision_sends_accepted_or_rejected_referral_task()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void PostOutcome_sends_procedure_or_follow_up_report()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void PostClosure_sends_task_and_provenance_and_reports_careloop_refusal()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Action_for_an_unknown_case_returns_404()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Action_with_a_non_synthetic_performer_id_returns_400()
    {
    }
}
