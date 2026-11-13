namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// Outbound mappers build CommunicationRequest, Task and DocumentReference.
/// </summary>
public sealed class OutboundMapperTests
{
    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Escalation_maps_to_a_Task_addressed_to_the_escalated_role()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Approved_summary_maps_to_a_DocumentReference()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Draft_summary_is_never_published()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Case_maps_to_a_Task_with_every_documented_field()
    {
    }
}
