using System.Net;
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

    [Fact]
    public async Task Acknowledging_updates_the_response_state_only_after_CareLoop_accepts()
    {
        using var simulator = new SimulatorFactory();
        await simulator.NotifyAsync();

        simulator.CareLoop.Refuse("acknowledgement", HttpStatusCode.UnprocessableEntity);
        var refused = await simulator.ActAsync("acknowledge");

        Assert.Equal(HttpStatusCode.BadGateway, refused.StatusCode);
        Assert.Equal(ResponseState.None, (await simulator.InboxMessageAsync()).Response);

        simulator.CareLoop.AnswerWith(HttpStatusCode.OK);
        var accepted = await simulator.ActAsync("acknowledge");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var message = await simulator.InboxMessageAsync();
        Assert.Equal(ResponseState.Acknowledged, message.Response);
        Assert.Equal(TestData.Now, message.RespondedAt);
    }

    [Fact]
    public async Task I_will_come_sends_one_acknowledgement_and_nothing_else()
    {
        using var simulator = new SimulatorFactory();
        await simulator.NotifyAsync();

        await simulator.ActAsync("acknowledge");
        await simulator.ActAsync("acknowledge");

        Assert.Equal(["delivery-status", "acknowledgement"], simulator.CareLoop.Categories);
    }

    [Fact]
    public async Task Understanding_updates_the_response_state_to_understood()
    {
        using var simulator = new SimulatorFactory();
        await simulator.NotifyAsync();

        var response = await simulator.ActAsync("understand");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ResponseState.Understood, (await simulator.InboxMessageAsync()).Response);
        Assert.Equal("teach-back", simulator.CareLoop.Categories[^1]);
    }

    [Fact]
    public async Task Call_me_updates_the_response_state_to_call_requested()
    {
        using var simulator = new SimulatorFactory();
        await simulator.NotifyAsync();

        await simulator.ActAsync("call-me");
        var response = await simulator.ActAsync("call-me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await simulator.InboxMessageAsync();
        Assert.Equal(ResponseState.CallRequested, message.Response);
        Assert.Equal(2, message.CallRequestCount);

        // A second request is its own event: same resource, next version.
        var versions = simulator.CareLoop.Callbacks.Where(c => c.Category == "call-me")
            .Select(c => c.Json.GetProperty("meta").GetProperty("versionId").GetString());
        Assert.Equal(["1", "2"], versions);
    }

    [Fact]
    public async Task Responding_to_an_unknown_message_returns_not_found()
    {
        using var simulator = new SimulatorFactory();

        Assert.Equal(HttpStatusCode.NotFound, (await simulator.ActAsync("acknowledge", "no-such-message")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await simulator.CreateClient().GetAsync("/api/inbox/no-such-message")).StatusCode);
        Assert.Empty(simulator.CareLoop.Calls);
    }

    [Fact]
    public async Task Delivered_message_stays_unacknowledged_until_the_patient_acts()
    {
        using var simulator = new SimulatorFactory();

        await simulator.NotifyAsync();

        var message = await simulator.InboxMessageAsync();
        Assert.Equal(DeliveryState.Delivered, message.Delivery);
        Assert.Equal(ResponseState.None, message.Response);
        Assert.Null(message.RespondedAt);
        Assert.Equal(["delivery-status"], simulator.CareLoop.Categories);
    }

    [Fact]
    public async Task A_message_that_failed_to_deliver_cannot_be_answered()
    {
        using var simulator = new SimulatorFactory();
        simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Fail, PatientResponseBehaviour.None, "SIMULATED: unreachable"));
        await simulator.NotifyAsync();

        var response = await simulator.ActAsync("acknowledge");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(["delivery-status"], simulator.CareLoop.Categories);
    }

    [Fact]
    public async Task A_caregiver_copy_is_reported_delivered_and_can_never_be_answered()
    {
        using var simulator = new SimulatorFactory();
        await simulator.NotifyAsync(inboxAddress: "SYN-CG-INBOX-0001", caregiver: true);
        var copyId = "caregiver-" + TestData.RequestId;

        foreach (var action in new[] { "acknowledge", "understand", "call-me" })
        {
            Assert.Equal(HttpStatusCode.Conflict, (await simulator.ActAsync(action, copyId)).StatusCode);
        }

        var report = Assert.Single(simulator.CareLoop.Callbacks);
        Assert.Equal("delivery-status", report.Category);
        Assert.Equal($"CommunicationRequest/{copyId}", report.Json.GetProperty("basedOn")[0].GetProperty("reference").GetString());
        Assert.Equal(ResponseState.None, (await simulator.InboxMessageAsync(copyId)).Response);
    }

    [Fact]
    public async Task A_patient_can_answer_a_message_the_channel_stayed_silent_about()
    {
        using var simulator = new SimulatorFactory();
        simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.StaySilent, PatientResponseBehaviour.None));
        await simulator.NotifyAsync();

        var response = await simulator.ActAsync("acknowledge");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["acknowledgement"], simulator.CareLoop.Categories);
    }
}
