namespace HisSimulator.Tests.Outbound;

/// <summary>Planned behaviour of <c>OutboundEventSender</c> (one path for scripted and manual events).</summary>
public sealed class OutboundEventSenderTests
{
    [Fact(Skip = "WS7: not implemented")]
    public void ScreeningResult_sends_report_and_creates_the_simulated_case()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void ConsultationUpdate_requested_sends_service_request_then_task()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void ReferralUpdate_later_state_sends_task_update_only()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Outcome_stores_the_outcome_resource_id_on_the_case()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Closure_sends_case_task_then_provenance()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Event_for_an_unknown_case_fails()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Rejection_by_careloop_is_reported_not_thrown()
    {
    }
}
