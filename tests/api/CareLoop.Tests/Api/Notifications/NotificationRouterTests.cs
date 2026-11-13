namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// Routes each domain event to the right recipient.
/// </summary>
public sealed class NotificationRouterTests
{
    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void NotificationRequested_goes_to_the_patient()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void HandoffProposed_goes_to_the_proposed_owner()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void HandoffRejected_goes_to_the_current_owner()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void EscalationRaised_goes_to_the_facility_lead_then_the_coordinator()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void CaseClosed_notifies_nobody()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Staff_notification_carries_no_patient_detail()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Handling_the_same_event_twice_sends_once()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Notifying_does_not_change_owner_or_deadline()
    {
    }
}
