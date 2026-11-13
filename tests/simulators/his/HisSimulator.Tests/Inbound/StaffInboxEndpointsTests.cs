namespace HisSimulator.Tests.Inbound;

/// <summary>Planned behaviour of the staff inbox routes.</summary>
public sealed class StaffInboxEndpointsTests
{
    [Fact(Skip = "WS7: not implemented")]
    public void GetStaffInbox_lists_items_newest_first()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void GetStaffInbox_filters_by_recipient()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void GetStaffInboxItem_returns_404_for_an_unknown_id()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void PostNotification_creates_a_staff_notification_item_without_patient_detail()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void PostNotification_without_recipient_or_message_returns_400()
    {
    }
}
