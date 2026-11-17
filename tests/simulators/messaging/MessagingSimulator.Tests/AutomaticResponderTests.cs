using System.Net;
using MessagingSimulator.State;

namespace MessagingSimulator.Tests;

/// <summary>
/// The automatic responder: the simulator plays the patient by reacting to what it receives (WS8).
/// </summary>
public sealed class AutomaticResponderTests : IDisposable
{
    private readonly SimulatorFactory _simulator = new();

    public void Dispose() => _simulator.Dispose();

    private static PatientBehaviour Responds(PatientResponseBehaviour response, TimeSpan after = default, int toNotification = 1) =>
        new(DeliveryBehaviour.Deliver, response, respondAfter: after, respondToNotification: toNotification);

    [Fact]
    public async Task Acknowledging_patient_replies_1_to_a_delivered_notification()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge));

        await _simulator.NotifyAsync();

        Assert.Equal(["delivered", "reply:1"], _simulator.CareLoop.Kinds);
        Assert.Equal("1", Assert.Single((await _simulator.InboxMessageAsync()).Replies).Text);
    }

    [Fact]
    public async Task Patient_who_wants_a_call_replies_2()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.CallMe));

        await _simulator.NotifyAsync();

        Assert.Equal(["delivered", "reply:2"], _simulator.CareLoop.Kinds);
        Assert.Equal("2", Assert.Single((await _simulator.InboxMessageAsync()).Replies).Text);
    }

    [Fact]
    public async Task Patient_replies_to_an_email_on_the_email_callback()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge), "SYN-EMAIL-0001");

        await _simulator.NotifyAsync(channel: "email", to: "SYN-EMAIL-0001");

        Assert.Equal(["delivered", "reply:1"], _simulator.CareLoop.Kinds);
        Assert.All(_simulator.CareLoop.Callbacks, c => Assert.Equal(SimulatorFactory.EmailCallbackUrl, c.Uri.ToString()));
    }

    [Fact]
    public async Task Silent_patient_stays_unacknowledged()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.None));

        await _simulator.NotifyAsync();
        _simulator.Clock.Advance(TimeSpan.FromDays(30));
        await _simulator.SendDueAsync();

        Assert.Equal(["delivered"], _simulator.CareLoop.Kinds);
        Assert.Empty((await _simulator.InboxMessageAsync()).Replies);
        Assert.Equal([PatientReactionKind.Delivered, PatientReactionKind.NoResponse], _simulator.Reactions().Select(r => r.Reaction));
    }

    [Fact]
    public async Task Nothing_is_sent_when_no_notification_was_received()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge));

        _simulator.Clock.Advance(TimeSpan.FromDays(1));
        var sent = await _simulator.SendDueAsync();

        Assert.Equal(0, sent);
        Assert.Empty(_simulator.CareLoop.Calls);
        Assert.Empty(_simulator.Reactions());
    }

    [Fact]
    public async Task Failed_delivery_gets_no_reply()
    {
        // Such a behaviour cannot be set through the routes; even if it is in force, a failed delivery is never replied to.
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Fail, PatientResponseBehaviour.Acknowledge, "SIMULATED: unreachable"));

        await _simulator.NotifyAsync();
        await _simulator.SendDueAsync();

        Assert.Equal(["failed"], _simulator.CareLoop.Kinds);
        Assert.Empty((await _simulator.InboxMessageAsync()).Replies);
    }

    [Fact]
    public async Task Unreported_delivery_gets_no_reply()
    {
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.StaySilent, PatientResponseBehaviour.Acknowledge));

        await _simulator.NotifyAsync();
        await _simulator.SendDueAsync();

        Assert.Empty(_simulator.CareLoop.Calls);
        Assert.Empty((await _simulator.InboxMessageAsync()).Replies);
    }

    [Fact]
    public async Task Reply_waits_for_respond_after_on_the_simulator_clock()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge, after: TimeSpan.FromSeconds(2)));
        await _simulator.NotifyAsync();

        _simulator.Clock.Advance(TimeSpan.FromSeconds(1.9));
        Assert.Equal(0, await _simulator.SendDueAsync());
        Assert.Equal(["delivered"], _simulator.CareLoop.Kinds);

        _simulator.Clock.Advance(TimeSpan.FromSeconds(0.1));
        Assert.Equal(1, await _simulator.SendDueAsync());
        Assert.Equal(0, await _simulator.SendDueAsync());

        Assert.Equal(["delivered", "reply:1"], _simulator.CareLoop.Kinds);
        Assert.Equal(TestData.Now.AddSeconds(2), Assert.Single((await _simulator.InboxMessageAsync()).Replies).SentAt);
    }

    [Fact]
    public async Task A_reply_still_waiting_is_dropped_when_the_behaviour_changes()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge, after: TimeSpan.FromSeconds(30)));
        await _simulator.NotifyAsync();
        Assert.Equal(1, _simulator.Store.CountPendingResponses());

        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.None));
        _simulator.Clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(0, await _simulator.SendDueAsync());
        Assert.Equal(["delivered"], _simulator.CareLoop.Kinds);
        Assert.Empty((await _simulator.InboxMessageAsync()).Replies);
    }

    [Fact]
    public async Task Patient_who_reacts_to_a_reminder_replies_to_the_second_notification_only()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge, toNotification: 2));

        await _simulator.NotifyAsync(messageId: "first");
        await _simulator.NotifyAsync(messageId: "caregiver-message-does-not-count", to: "SYN-CG-EMAIL-0001", channel: "email", audience: "caregiver");
        await _simulator.NotifyAsync(messageId: "staff-message-does-not-count", to: TestData.PatientId, audience: "staff");
        await _simulator.NotifyAsync(messageId: "reminder");
        await _simulator.NotifyAsync(messageId: "third");

        Assert.Empty((await _simulator.InboxMessageAsync("first")).Replies);
        Assert.Single((await _simulator.InboxMessageAsync("reminder")).Replies);
        Assert.Empty((await _simulator.InboxMessageAsync("third")).Replies);
    }

    [Fact]
    public async Task Notifications_about_another_case_are_counted_separately()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge, toNotification: 2));

        await _simulator.NotifyAsync(messageId: "case-a-first", caseId: "case-a");
        await _simulator.NotifyAsync(messageId: "case-b-first", caseId: "case-b");
        await _simulator.NotifyAsync(messageId: "case-a-reminder", caseId: "case-a");

        Assert.Empty((await _simulator.InboxMessageAsync("case-b-first")).Replies);
        Assert.Single((await _simulator.InboxMessageAsync("case-a-reminder")).Replies);
    }

    [Fact]
    public async Task Resent_notification_is_not_replied_to_twice()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.CallMe));

        await _simulator.NotifyAsync();
        await _simulator.NotifyAsync();
        await _simulator.NotifyAsync();

        Assert.Equal(["delivered", "reply:2"], _simulator.CareLoop.Kinds);
        Assert.Single((await _simulator.InboxMessageAsync()).Replies);
    }

    [Fact]
    public async Task Automatic_reply_is_recorded_as_an_automatic_reaction()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge));
        await _simulator.NotifyAsync(messageId: "automatic");
        _simulator.Store.RemoveBehaviour(TestData.PatientId);
        await _simulator.NotifyAsync(messageId: "by-hand");
        await _simulator.ReplyAsync("1", "by-hand");

        var replies = _simulator.Reactions().Where(r => r.Reaction == PatientReactionKind.Replied).ToList();

        Assert.Equal(2, replies.Count);
        Assert.True(replies.Single(r => r.MessageId == "automatic").Automatic);
        Assert.False(replies.Single(r => r.MessageId == "by-hand").Automatic);
        Assert.All(replies, r => Assert.True(r.Succeeded));
        Assert.Equal("automatic-r1", replies.Single(r => r.MessageId == "automatic").SourceEventId);
    }

    [Fact]
    public async Task Reply_rejected_by_CareLoop_is_recorded_as_failed()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge));
        _simulator.CareLoop.RefuseReplies(HttpStatusCode.UnprocessableEntity);

        var response = await _simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reaction = _simulator.Reactions().Single(r => r.Reaction == PatientReactionKind.Replied);
        Assert.False(reaction.Succeeded);
        Assert.Equal(422, reaction.HttpStatusCode);
        Assert.NotNull(reaction.Error);
        Assert.Empty((await _simulator.InboxMessageAsync()).Replies);

        // A refusal is CareLoop's decision, so it is not retried.
        Assert.Single(_simulator.CareLoop.Callbacks, c => c.Type == "reply");
    }

    [Fact]
    public void A_patient_can_only_stay_silent_say_they_will_come_or_ask_for_a_call()
    {
        // Teach-back and a confidence rating are not things a patient does by text or email:
        // a nurse records them after a call, in CareLoop.
        Assert.Equal(
            [PatientResponseBehaviour.None, PatientResponseBehaviour.Acknowledge, PatientResponseBehaviour.CallMe],
            Enum.GetValues<PatientResponseBehaviour>());
    }
}
