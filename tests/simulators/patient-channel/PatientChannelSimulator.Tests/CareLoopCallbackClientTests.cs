using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>What the simulator sends back to CareLoop, and how it is signed and retried (WS8).</summary>
public sealed class CareLoopCallbackClientTests
{
    [Fact]
    public async Task Callback_is_signed_over_the_exact_bytes_sent()
    {
        using var simulator = new SimulatorFactory();

        await simulator.NotifyAsync();

        var call = Assert.Single(simulator.CareLoop.Callbacks);
        var expected = "sha256=" + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(SimulatorFactory.Secret), call.Body));
        Assert.Equal(expected, call.Signature);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal(SimulatorFactory.SmsCallbackUrl, call.Uri.ToString());
        Assert.Equal("application/json; charset=utf-8", call.ContentType);

        // No byte-order mark: CareLoop hashes the body as text, so a BOM would break the signature.
        Assert.Equal((byte)'{', call.Body[0]);
    }

    [Fact]
    public void Signature_matches_the_scheme_CareLoop_documents()
    {
        // HMAC-SHA256 test vector from RFC 4231, test case 2.
        var signature = CareLoopCallbackClient.SignatureFor(Encoding.UTF8.GetBytes("what do ya want for nothing?"), "Jefe");

        Assert.Equal("sha256=5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843", signature);
    }

    [Fact]
    public async Task Reply_is_signed_like_a_delivery_report()
    {
        using var simulator = new SimulatorFactory();
        await simulator.NotifyAsync();

        await simulator.ReplyAsync("1");

        var reply = simulator.CareLoop.Callbacks[^1];
        Assert.Equal("reply", reply.Type);
        Assert.Equal(CareLoopCallbackClient.SignatureFor(reply.Body, SimulatorFactory.Secret), reply.Signature);
    }

    [Fact]
    public async Task Without_a_secret_nothing_is_sent_and_the_reason_is_recorded()
    {
        using var simulator = new SimulatorFactory(secret: string.Empty);

        var response = await simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Empty(simulator.CareLoop.Calls);
        var reaction = Assert.Single(simulator.Reactions());
        Assert.False(reaction.Succeeded);
        Assert.Contains("CareLoop:InboundSecret", reaction.Error, StringComparison.Ordinal);
        Assert.Equal(DeliveryState.Pending, simulator.Store.FindInboxMessage(TestData.MessageId)!.Delivery);
    }

    [Fact]
    public async Task Server_error_is_retried_with_the_same_bytes_and_then_succeeds()
    {
        using var simulator = new SimulatorFactory();
        var answers = new Queue<HttpStatusCode>([HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK]);
        simulator.CareLoop.Answer = _ => new HttpResponseMessage(answers.Dequeue());

        await simulator.NotifyAsync();

        var calls = simulator.CareLoop.Callbacks;
        Assert.Equal(3, calls.Count);
        Assert.All(calls, c => Assert.Equal(calls[0].Body, c.Body));
        Assert.All(calls, c => Assert.Equal(calls[0].Signature, c.Signature));
        Assert.Equal(DeliveryState.Delivered, (await simulator.InboxMessageAsync()).Delivery);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task Refusal_is_not_retried(HttpStatusCode status)
    {
        using var simulator = new SimulatorFactory();
        simulator.CareLoop.AnswerWith(status);

        await simulator.NotifyAsync();

        Assert.Single(simulator.CareLoop.Callbacks);
        var reaction = Assert.Single(simulator.Reactions());
        Assert.False(reaction.Succeeded);
        Assert.Equal((int)status, reaction.HttpStatusCode);
        Assert.Equal(DeliveryState.Pending, (await simulator.InboxMessageAsync()).Delivery);
    }

    [Fact]
    public async Task The_simulator_only_ever_posts_to_CareLoop_and_reads_nothing_from_it()
    {
        using var simulator = new SimulatorFactory();
        await simulator.NotifyAsync();
        await simulator.NotifyAsync(messageId: "mail", channel: "email", to: "SYN-EMAIL-0001");
        await simulator.ReplyAsync("2");

        Assert.All(simulator.CareLoop.Calls, c => Assert.Equal(HttpMethod.Post, c.Method));
        Assert.All(simulator.CareLoop.Calls, c => Assert.StartsWith(SimulatorFactory.CallbackBaseUrl + "/", c.Uri.ToString(), StringComparison.Ordinal));
        Assert.All(simulator.CareLoop.Calls, c => Assert.DoesNotContain("/fhir", c.Uri.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Messages_and_callbacks_are_listed_in_the_log_newest_first()
    {
        using var simulator = new SimulatorFactory();
        await simulator.NotifyAsync();
        simulator.Clock.Advance(TimeSpan.FromMinutes(1));
        await simulator.ReplyAsync("2");

        var log = await simulator.CreateClient().GetFromJsonAsync<List<MessageLogEntry>>("/api/message-log", SimulatorFactory.Json);

        Assert.Equal(3, log!.Count);
        Assert.Equal(("POST", "reply", 200), (log[0].Method, log[0].Kind, log[0].StatusCode));
        Assert.Equal(TestData.MessageId, log[0].MessageId);
        Assert.Contains("\"text\":\"2\"", log[0].Body, StringComparison.Ordinal);
        Assert.All(log, e => Assert.True(e.Simulated));
    }

    [Fact]
    public async Task OpenApi_document_is_generated()
    {
        using var simulator = new SimulatorFactory();

        var response = await simulator.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/inbox", document, StringComparison.Ordinal);
        Assert.Contains("/messages", document, StringComparison.Ordinal);
        Assert.DoesNotContain("/fhir", document, StringComparison.OrdinalIgnoreCase);
    }
}
