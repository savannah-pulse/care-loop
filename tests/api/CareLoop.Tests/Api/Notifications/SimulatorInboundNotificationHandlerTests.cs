namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// Translates what the patient-channel simulator sends back (Fhir/Samples/Communication.json).
/// </summary>
public sealed class SimulatorInboundNotificationHandlerTests
{
    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Completed_translates_to_delivered()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Not_done_translates_to_failed_with_reason()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Patient_acknowledgement_category_translates_to_acknowledged()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Teach_back_category_translates_to_understood()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Call_me_category_translates_to_call_requested()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Delivery_report_is_never_translated_to_an_acknowledgement()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Same_communication_version_gives_the_same_source_event_id()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Body_that_is_not_a_Communication_is_a_format_error()
    {
    }
}
