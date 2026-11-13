namespace HisSimulator.Tests.Inbound;

/// <summary>Planned behaviour of <c>POST /fhir/{resourceType}</c> and <c>PUT /fhir/{resourceType}/{id}</c>.</summary>
public sealed class FhirInboundEndpointsTests
{
    [Fact(Skip = "WS7: not implemented")]
    public void Post_escalation_task_returns_201_and_adds_an_inbox_item()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Post_handoff_proposed_task_adds_an_inbox_item_for_the_proposed_owner()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Post_call_request_task_adds_an_inbox_item_for_the_current_owner()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Post_document_reference_adds_a_handoff_summary_item()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Put_task_update_records_a_rejection_item()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Post_malformed_fhir_returns_400()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Post_mismatched_resource_type_returns_400()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Post_unsupported_resource_type_returns_422()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Every_received_message_is_logged_as_received()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Receiving_a_handoff_does_not_send_an_acceptance()
    {
    }
}
