using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>Planned WS8 behaviours: inbox state.</summary>
public class InboxStateTests
{
    [Fact]
    public void Inbox_lists_messages_newest_first()
    {
        var store = new SimulatorStore();
        store.AddInboxMessage(TestData.Message(requestId: "middle", receivedAt: TestData.Now.AddMinutes(1)));
        store.AddInboxMessage(TestData.Message(requestId: "newest", receivedAt: TestData.Now.AddMinutes(2)));
        store.AddInboxMessage(TestData.Message(requestId: "oldest", receivedAt: TestData.Now));
        store.AddInboxMessage(TestData.Message(requestId: "oldest-later-arrival", receivedAt: TestData.Now));

        Assert.Equal(
            ["newest", "middle", "oldest-later-arrival", "oldest"],
            store.GetInboxMessages().Select(m => m.MessageId));
    }

    [Fact]
    public void Adding_a_duplicate_message_id_is_rejected()
    {
        var store = new SimulatorStore();
        store.AddInboxMessage(TestData.Message());

        Assert.Throws<InvalidOperationException>(() => store.AddInboxMessage(TestData.Message()));
        Assert.Single(store.GetInboxMessages());
    }

    [Fact(Skip = "WS8: not implemented")]
    public void Acknowledging_updates_the_response_state_only_after_CareLoop_accepts() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Understanding_updates_the_response_state_to_understood() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Call_me_updates_the_response_state_to_call_requested() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Responding_to_an_unknown_message_returns_not_found() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Delivered_message_stays_unacknowledged_until_the_patient_acts() { }
}
