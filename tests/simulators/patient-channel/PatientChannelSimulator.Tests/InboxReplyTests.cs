using System.Net;
using System.Text.Json;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>Replying to a text message or an email as the simulated patient (WS8).</summary>
public sealed class InboxReplyTests : IDisposable
{
    private readonly SimulatorFactory _simulator = new();

    public void Dispose() => _simulator.Dispose();

    [Fact]
    public async Task Inbox_lists_messages_newest_first()
    {
        await _simulator.NotifyAsync(messageId: "older");
        _simulator.Clock.Advance(TimeSpan.FromMinutes(1));
        await _simulator.NotifyAsync(messageId: "newer");

        Assert.Equal(["newer", "older"], _simulator.Store.GetInboxMessages().Select(m => m.MessageId));
    }

    [Fact]
    public async Task Delivered_message_has_no_reply_until_the_patient_sends_one()
    {
        await _simulator.NotifyAsync();

        var message = await _simulator.InboxMessageAsync();
        Assert.Equal(DeliveryState.Delivered, message.Delivery);
        Assert.Empty(message.Replies);
        Assert.Equal(["delivered"], _simulator.CareLoop.Kinds);
    }

    [Theory]
    [InlineData("sms", SimulatorFactory.SmsCallbackUrl)]
    [InlineData("email", SimulatorFactory.EmailCallbackUrl)]
    public async Task Reply_is_sent_to_CareLoop_on_the_channel_the_message_came_on(string channel, string callbackUrl)
    {
        await _simulator.NotifyAsync(channel: channel);

        var response = await _simulator.ReplyAsync("1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reply = _simulator.CareLoop.Callbacks[^1];
        Assert.Equal(callbackUrl, reply.Uri.ToString());
        Assert.Equal("reply", reply.Type);
        Assert.Equal("1", reply.ReplyText);
        Assert.Equal(TestData.MessageId, reply.Json.GetProperty("inReplyTo").GetString());
        Assert.Equal(TestData.PatientId, reply.Json.GetProperty("from").GetString());
        Assert.Equal($"{TestData.MessageId}-r1", reply.Json.GetProperty("replyId").GetString());
        Assert.Equal(TestData.Now, reply.Json.GetProperty("at").GetDateTimeOffset());
        Assert.Equal(TestData.CaseId, reply.Json.GetProperty("metadata").GetProperty("caseId").GetString());
        Assert.False(reply.Json.TryGetProperty("status", out _));
        Assert.False(reply.Json.TryGetProperty("resourceType", out _));

        var stored = Assert.Single((await _simulator.InboxMessageAsync()).Replies);
        Assert.Equal(new InboxReply($"{TestData.MessageId}-r1", "1", TestData.Now), stored);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("  I cannot come on Tuesday, can someone call me?  ")]
    [InlineData("3")]
    [InlineData("understood")]
    public async Task Reply_is_forwarded_exactly_as_typed_whatever_it_says(string text)
    {
        await _simulator.NotifyAsync();

        var response = await _simulator.ReplyAsync(text);

        // What a reply means is CareLoop's decision: the simulator refuses none for its wording and interprets none.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(text, _simulator.CareLoop.Callbacks[^1].ReplyText);
        Assert.Equal(text, Assert.Single((await _simulator.InboxMessageAsync()).Replies).Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_reply_is_not_sent(string? text)
    {
        await _simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await _simulator.ReplyAsync(text)).StatusCode);
        Assert.Equal(["delivered"], _simulator.CareLoop.Kinds);
    }

    [Fact]
    public async Task Each_reply_gets_its_own_identifier_even_when_two_arrive_at_once()
    {
        await _simulator.NotifyAsync();

        await Task.WhenAll(Task.Run(() => _simulator.ReplyAsync("2")), Task.Run(() => _simulator.ReplyAsync("2")));
        await _simulator.ReplyAsync("1");

        var ids = _simulator.CareLoop.Callbacks.Where(c => c.Type == "reply")
            .Select(c => c.Json.GetProperty("replyId").GetString())
            .ToList();
        Assert.Equal([$"{TestData.MessageId}-r1", $"{TestData.MessageId}-r2", $"{TestData.MessageId}-r3"], ids.Order());
        Assert.Equal(3, (await _simulator.InboxMessageAsync()).Replies.Count);
    }

    [Fact]
    public async Task Reply_refused_by_CareLoop_is_not_shown_in_the_inbox()
    {
        await _simulator.NotifyAsync();

        _simulator.CareLoop.RefuseReplies(HttpStatusCode.UnprocessableEntity);
        var refused = await _simulator.ReplyAsync("1");

        Assert.Equal(HttpStatusCode.BadGateway, refused.StatusCode);
        Assert.Empty((await _simulator.InboxMessageAsync()).Replies);
        var reaction = _simulator.Reactions().Single(r => r.Reaction == PatientReactionKind.Replied);
        Assert.False(reaction.Succeeded);
        Assert.False(reaction.Automatic);
        Assert.Equal(422, reaction.HttpStatusCode);

        // CareLoop may have stored the refused reply under its identifier, so the next one takes a new
        // identifier; reusing it would get the new reply dropped as a resend.
        _simulator.CareLoop.AnswerWith(HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, (await _simulator.ReplyAsync("1")).StatusCode);
        Assert.Equal($"{TestData.MessageId}-r2", Assert.Single((await _simulator.InboxMessageAsync()).Replies).ReplyId);
    }

    [Fact]
    public async Task Reply_to_an_unknown_message_is_not_found()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _simulator.ReplyAsync("1", "no-such-message")).StatusCode);
        Assert.Empty(_simulator.CareLoop.Calls);
    }

    [Fact]
    public async Task Message_whose_delivery_failed_cannot_be_replied_to()
    {
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Fail, PatientResponseBehaviour.None, "SIMULATED: handset unreachable"));
        await _simulator.NotifyAsync();

        var response = await _simulator.ReplyAsync("1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(["failed"], _simulator.CareLoop.Kinds);
    }

    [Theory]
    [InlineData("caregiver")]
    [InlineData("staff")]
    public async Task Only_a_message_to_the_patient_can_be_replied_to(string audience)
    {
        await _simulator.NotifyAsync(to: "SYN-EMAIL-OTHER-0001", channel: "email", audience: audience);

        foreach (var text in new[] { "1", "2", "thank you" })
        {
            Assert.Equal(HttpStatusCode.Conflict, (await _simulator.ReplyAsync(text)).StatusCode);
        }

        // It was delivered and reported; nothing was ever sent as a reply.
        Assert.Equal(["delivered"], _simulator.CareLoop.Kinds);
        Assert.Empty((await _simulator.InboxMessageAsync()).Replies);
    }

    [Fact]
    public async Task Message_the_channel_stayed_silent_about_can_still_be_replied_to()
    {
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.StaySilent, PatientResponseBehaviour.None));
        await _simulator.NotifyAsync();

        var response = await _simulator.ReplyAsync("1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["reply:1"], _simulator.CareLoop.Kinds);
    }

    [Fact]
    public async Task There_is_no_acknowledge_teach_back_call_me_or_rating_action()
    {
        await _simulator.NotifyAsync();
        var client = _simulator.CreateClient();

        foreach (var action in new[] { "acknowledge", "understand", "call-me", "rate-confidence" })
        {
            var response = await client.PostAsync($"/api/inbox/{TestData.MessageId}/{action}", JsonContent(new { rating = 4 }));
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        }

        Assert.Equal(["delivered"], _simulator.CareLoop.Kinds);
    }

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json");
}
