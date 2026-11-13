namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// FhirPublisher publishes each domain event as the documented FHIR message.
/// </summary>
public sealed class FhirPublisherTests
{
    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void NotificationRequested_publishes_nothing()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void HandoffProposed_sends_a_Task_and_a_DocumentReference()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void DeadlineApproaching_publishes_nothing()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void CaseClosed_sends_the_case_task_completed()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Handling_the_same_event_twice_sends_the_same_resource_id()
    {
    }
}
