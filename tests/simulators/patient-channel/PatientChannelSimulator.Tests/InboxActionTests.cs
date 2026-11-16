using System.Net;
using System.Net.Http.Json;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>The SMS reply (C7), the confidence rating (C10) and caregiver copies (C13), through the real routes (WS8).</summary>
public sealed class InboxActionTests : IDisposable
{
    private const string SmsInbox = "SYN-SMS-0001";

    private readonly SimulatorFactory _simulator = new();

    public void Dispose() => _simulator.Dispose();

    private Task<HttpResponseMessage> ReplyAsync(string? text, string messageId = TestData.RequestId) =>
        _simulator.CreateClient().PostAsJsonAsync($"/api/inbox/{messageId}/reply", new { text });

    private Task<HttpResponseMessage> RateAsync(int rating, string messageId = TestData.RequestId) =>
        _simulator.CreateClient().PostAsJsonAsync($"/api/inbox/{messageId}/rate-confidence", new { rating });

    [Theory]
    [InlineData("1")]
    [InlineData(" 1\n")]
    public async Task Sms_reply_1_acknowledges(string text)
    {
        await _simulator.NotifyAsync(inboxAddress: SmsInbox);

        var response = await ReplyAsync(text);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["delivery-status", "acknowledgement"], _simulator.CareLoop.Categories);
        Assert.Equal(ResponseState.Acknowledged, (await _simulator.InboxMessageAsync()).Response);
    }

    [Fact]
    public async Task A_call_request_after_an_acknowledgement_does_not_hide_it_or_cause_a_resend()
    {
        await _simulator.NotifyAsync();
        await _simulator.ActAsync("acknowledge");
        await _simulator.ActAsync("call-me");

        var message = await _simulator.InboxMessageAsync();
        Assert.Equal(ResponseState.Acknowledged, message.Response);
        Assert.NotNull(message.AcknowledgedAt);
        Assert.Equal(1, message.CallRequestCount);

        await _simulator.ActAsync("acknowledge");
        Assert.Equal(["delivery-status", "acknowledgement", "call-me"], _simulator.CareLoop.Categories);
    }

    [Fact]
    public async Task Two_call_requests_at_once_are_two_requests_with_their_own_versions()
    {
        await _simulator.NotifyAsync();

        await Task.WhenAll(Task.Run(() => _simulator.ActAsync("call-me")), Task.Run(() => _simulator.ActAsync("call-me")));

        var versions = _simulator.CareLoop.Callbacks
            .Where(c => c.Category == "call-me")
            .Select(c => c.Json.GetProperty("meta").GetProperty("versionId").GetString())
            .Order()
            .ToList();
        Assert.Equal(["1", "2"], versions);
        Assert.Equal(2, (await _simulator.InboxMessageAsync()).CallRequestCount);
    }

    [Fact]
    public async Task Sms_reply_2_asks_for_a_call_and_does_not_acknowledge()
    {
        await _simulator.NotifyAsync(inboxAddress: SmsInbox);

        var response = await ReplyAsync("2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["delivery-status", "call-me"], _simulator.CareLoop.Categories);
        Assert.Equal(ResponseState.CallRequested, (await _simulator.InboxMessageAsync()).Response);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("yes")]
    [InlineData("12")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Sms_reply_with_any_other_text_is_refused_and_sends_nothing(string? text)
    {
        await _simulator.NotifyAsync(inboxAddress: SmsInbox);

        var response = await ReplyAsync(text);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["delivery-status"], _simulator.CareLoop.Categories);
        Assert.Equal(ResponseState.None, (await _simulator.InboxMessageAsync()).Response);
    }

    [Fact]
    public async Task Text_reply_is_only_for_the_sms_inbox()
    {
        await _simulator.NotifyAsync(requestId: "app");
        await _simulator.NotifyAsync(requestId: "copy", inboxAddress: "SYN-CG-INBOX-0001", caregiver: true);

        Assert.Equal(HttpStatusCode.Conflict, (await ReplyAsync("1", "app")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ReplyAsync("1", "caregiver-copy")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ReplyAsync("1", "no-such-message")).StatusCode);
        Assert.DoesNotContain("acknowledgement", _simulator.CareLoop.Categories);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task Rating_is_sent_and_shown_on_the_message(int rating)
    {
        await _simulator.NotifyAsync();

        var response = await RateAsync(rating);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var call = _simulator.CareLoop.Callbacks[^1];
        Assert.Equal("confidence-rating", call.Category);
        Assert.Equal(rating.ToString(System.Globalization.CultureInfo.InvariantCulture), call.Json.GetProperty("payload")[0].GetProperty("contentString").GetString());
        var message = await _simulator.InboxMessageAsync();
        Assert.Equal(rating, message.Confidence);
        Assert.Equal(TestData.Now, message.ConfidenceRatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-3)]
    public async Task Rating_outside_one_to_five_is_refused_and_sends_nothing(int rating)
    {
        await _simulator.NotifyAsync();

        var response = await RateAsync(rating);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["delivery-status"], _simulator.CareLoop.Categories);
        Assert.Null((await _simulator.InboxMessageAsync()).Confidence);
    }

    [Fact]
    public async Task Rating_is_not_an_acknowledgement_and_never_replaces_one()
    {
        await _simulator.NotifyAsync();

        await RateAsync(3);
        var ratedOnly = await _simulator.InboxMessageAsync();
        await _simulator.ActAsync("acknowledge");
        await RateAsync(4);
        var acknowledgedThenRated = await _simulator.InboxMessageAsync();

        Assert.Equal(ResponseState.None, ratedOnly.Response);
        Assert.Null(ratedOnly.RespondedAt);
        Assert.Equal(ResponseState.Acknowledged, acknowledgedThenRated.Response);
        Assert.Equal(4, acknowledgedThenRated.Confidence);
        Assert.Equal(2, acknowledgedThenRated.ConfidenceRatingCount);
        Assert.Equal(["delivery-status", "confidence-rating", "acknowledgement", "confidence-rating"], _simulator.CareLoop.Categories);

        var reactions = _simulator.Reactions().Select(r => r.Reaction).ToList();
        Assert.Equal(2, reactions.Count(r => r == PatientReactionKind.ConfidenceRated));
        Assert.Single(reactions, r => r == PatientReactionKind.Acknowledged);
    }

    [Fact]
    public async Task Rating_refused_by_CareLoop_leaves_the_message_unrated()
    {
        await _simulator.NotifyAsync();
        _simulator.CareLoop.Refuse("confidence-rating", HttpStatusCode.UnprocessableEntity);

        var response = await RateAsync(3);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Null((await _simulator.InboxMessageAsync()).Confidence);
    }

    [Fact]
    public async Task Caregiver_copy_lands_in_email_is_never_answered_and_cannot_be_rated()
    {
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Deliver, PatientResponseBehaviour.Acknowledge));
        await _simulator.NotifyAsync(inboxAddress: "SYN-CG-INBOX-0001", caregiver: true);
        var copyId = "caregiver-" + TestData.RequestId;

        var rating = await RateAsync(5, copyId);

        var copy = await _simulator.InboxMessageAsync(copyId);
        Assert.Equal(InboxChannel.Email, copy.Channel);
        Assert.Equal(DeliveryState.Delivered, copy.Delivery);
        Assert.Equal(ResponseState.None, copy.Response);
        Assert.Equal(HttpStatusCode.Conflict, rating.StatusCode);
        Assert.Equal(["delivery-status"], _simulator.CareLoop.Categories);
    }
}
