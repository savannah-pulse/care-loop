using System.Net;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>
/// The automatic responder: the simulator plays the patient by reacting to what it receives (WS8).
/// </summary>
public sealed class AutomaticResponderTests : IDisposable
{
    private readonly SimulatorFactory _simulator = new();

    public void Dispose() => _simulator.Dispose();

    private static PatientBehaviour Responds(PatientResponseBehaviour response, TimeSpan after = default, int toNotification = 1, int? confidence = null) =>
        new(DeliveryBehaviour.Deliver, response, respondAfter: after, respondToNotification: toNotification, confidence: confidence);

    [Fact]
    public async Task Acknowledging_patient_acknowledges_a_delivered_notification()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge));

        await _simulator.NotifyAsync();

        Assert.Equal(["delivery-status", "acknowledgement"], _simulator.CareLoop.Categories);
        Assert.Equal(ResponseState.Acknowledged, (await _simulator.InboxMessageAsync()).Response);
    }

    [Fact]
    public async Task Understanding_patient_sends_a_teach_back_response()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Understand));

        await _simulator.NotifyAsync();

        Assert.Equal(["delivery-status", "teach-back"], _simulator.CareLoop.Categories);
        Assert.Equal(ResponseState.Understood, (await _simulator.InboxMessageAsync()).Response);
    }

    [Fact]
    public async Task Call_me_patient_asks_for_a_call()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.CallMe));

        await _simulator.NotifyAsync();

        Assert.Equal(["delivery-status", "call-me"], _simulator.CareLoop.Categories);
        Assert.Equal(ResponseState.CallRequested, (await _simulator.InboxMessageAsync()).Response);
    }

    [Fact]
    public async Task Silent_patient_never_responds()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.None));

        await _simulator.NotifyAsync();
        _simulator.Clock.Advance(TimeSpan.FromDays(30));
        await _simulator.SendDueAsync();

        Assert.Equal(["delivery-status"], _simulator.CareLoop.Categories);
        Assert.Equal(ResponseState.None, (await _simulator.InboxMessageAsync()).Response);
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
    public async Task Failed_delivery_gets_no_response()
    {
        // Such a behaviour cannot be set through the routes; even if it is in force, a failed delivery is never answered.
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Fail, PatientResponseBehaviour.Acknowledge, "SIMULATED: unreachable"));

        await _simulator.NotifyAsync();
        await _simulator.SendDueAsync();

        Assert.Equal(["delivery-status"], _simulator.CareLoop.Categories);
        Assert.Equal(ResponseState.None, (await _simulator.InboxMessageAsync()).Response);
    }

    [Fact]
    public async Task Unreported_delivery_gets_no_response()
    {
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.StaySilent, PatientResponseBehaviour.Acknowledge));

        await _simulator.NotifyAsync();
        await _simulator.SendDueAsync();

        Assert.Empty(_simulator.CareLoop.Calls);
        Assert.Equal(ResponseState.None, (await _simulator.InboxMessageAsync()).Response);
    }

    [Fact]
    public async Task Response_waits_for_respond_after_on_the_simulator_clock()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge, after: TimeSpan.FromSeconds(2)));
        await _simulator.NotifyAsync();

        _simulator.Clock.Advance(TimeSpan.FromSeconds(1.9));
        Assert.Equal(0, await _simulator.SendDueAsync());
        Assert.Equal(["delivery-status"], _simulator.CareLoop.Categories);

        _simulator.Clock.Advance(TimeSpan.FromSeconds(0.1));
        Assert.Equal(1, await _simulator.SendDueAsync());
        Assert.Equal(0, await _simulator.SendDueAsync());

        Assert.Equal(["delivery-status", "acknowledgement"], _simulator.CareLoop.Categories);
        var message = await _simulator.InboxMessageAsync();
        Assert.Equal(ResponseState.Acknowledged, message.Response);
        Assert.Equal(TestData.Now.AddSeconds(2), message.RespondedAt);
    }

    [Fact]
    public async Task A_response_still_waiting_is_dropped_when_the_behaviour_changes()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge, after: TimeSpan.FromSeconds(30)));
        await _simulator.NotifyAsync();
        Assert.Equal(1, _simulator.Store.CountPendingResponses());

        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.None));
        _simulator.Clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(0, await _simulator.SendDueAsync());
        Assert.Equal(["delivery-status"], _simulator.CareLoop.Categories);
        Assert.Equal(ResponseState.None, (await _simulator.InboxMessageAsync()).Response);
    }

    [Fact]
    public async Task Patient_who_responds_to_the_second_notification_ignores_the_first()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge, toNotification: 2));

        await _simulator.NotifyAsync(requestId: "first");
        await _simulator.NotifyAsync(requestId: "caregiver-copy-does-not-count", inboxAddress: "SYN-CG-INBOX-0001", caregiver: true);
        await _simulator.NotifyAsync(requestId: "reminder");
        await _simulator.NotifyAsync(requestId: "third");

        Assert.Equal(ResponseState.None, (await _simulator.InboxMessageAsync("first")).Response);
        Assert.Equal(ResponseState.Acknowledged, (await _simulator.InboxMessageAsync("reminder")).Response);
        Assert.Equal(ResponseState.None, (await _simulator.InboxMessageAsync("third")).Response);
    }

    [Fact]
    public async Task Resent_notification_does_not_trigger_a_second_response()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.CallMe));

        await _simulator.NotifyAsync();
        await _simulator.NotifyAsync();
        await _simulator.NotifyAsync();

        Assert.Equal(["delivery-status", "call-me"], _simulator.CareLoop.Categories);
        Assert.Equal(1, (await _simulator.InboxMessageAsync()).CallRequestCount);
    }

    [Fact]
    public async Task Automatic_response_is_recorded_as_an_automatic_reaction()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge));
        await _simulator.NotifyAsync(requestId: "automatic");
        _simulator.Store.RemoveBehaviour(TestData.PatientId);
        await _simulator.NotifyAsync(requestId: "by-hand");
        await _simulator.ActAsync("acknowledge", "by-hand");

        var acknowledgements = _simulator.Reactions().Where(r => r.Reaction == PatientReactionKind.Acknowledged).ToList();

        Assert.Equal(2, acknowledgements.Count);
        Assert.True(acknowledgements.Single(r => r.MessageId == "automatic").Automatic);
        Assert.False(acknowledgements.Single(r => r.MessageId == "by-hand").Automatic);
        Assert.All(acknowledgements, r => Assert.True(r.Succeeded));
        Assert.Equal("Communication/automatic-acknowledgement/_history/1", acknowledgements.Single(r => r.MessageId == "automatic").SourceEventId);
    }

    [Fact]
    public async Task Response_rejected_by_CareLoop_is_recorded_as_failed()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge));
        _simulator.CareLoop.Refuse("acknowledgement", HttpStatusCode.UnprocessableEntity);

        var response = await _simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reaction = _simulator.Reactions().Single(r => r.Reaction == PatientReactionKind.Acknowledged);
        Assert.False(reaction.Succeeded);
        Assert.Equal(422, reaction.HttpStatusCode);
        Assert.NotNull(reaction.Error);
        Assert.Equal(ResponseState.None, (await _simulator.InboxMessageAsync()).Response);

        // A refusal is CareLoop's decision, so it is not retried.
        Assert.Single(_simulator.CareLoop.Callbacks, c => c.Category == "acknowledgement");
    }

    [Fact]
    public async Task Patient_who_acknowledges_with_a_rating_sends_both_and_stays_acknowledged()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.Acknowledge, confidence: 4));

        await _simulator.NotifyAsync();

        Assert.Equal(["delivery-status", "acknowledgement", "confidence-rating"], _simulator.CareLoop.Categories);
        var message = await _simulator.InboxMessageAsync();
        Assert.Equal(ResponseState.Acknowledged, message.Response);
        Assert.Equal(4, message.Confidence);
        Assert.Equal("4", _simulator.CareLoop.Callbacks[^1].Json.GetProperty("payload")[0].GetProperty("contentString").GetString());
    }

    [Fact]
    public async Task Patient_who_only_rates_is_not_acknowledged()
    {
        _simulator.SetBehaviour(Responds(PatientResponseBehaviour.RateConfidence, after: TimeSpan.FromSeconds(1), confidence: 2));
        await _simulator.NotifyAsync();
        _simulator.Clock.Advance(TimeSpan.FromSeconds(1));

        await _simulator.SendDueAsync();

        Assert.Equal(["delivery-status", "confidence-rating"], _simulator.CareLoop.Categories);
        var message = await _simulator.InboxMessageAsync();
        Assert.Equal(ResponseState.None, message.Response);
        Assert.Null(message.RespondedAt);
        Assert.Equal(2, message.Confidence);
        Assert.Equal(PatientReactionKind.ConfidenceRated, _simulator.Reactions()[^1].Reaction);
    }
}
