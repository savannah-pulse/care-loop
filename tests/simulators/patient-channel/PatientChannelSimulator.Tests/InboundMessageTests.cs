using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>Receiving a text message or an email from CareLoop, through the real route (WS8).</summary>
public sealed class InboundMessageTests : IDisposable
{
    private readonly SimulatorFactory _simulator = new();

    public void Dispose() => _simulator.Dispose();

    [Fact]
    public async Task Message_lands_in_the_inbox_marked_simulated()
    {
        var response = await _simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var accepted = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(TestData.MessageId, accepted.RootElement.GetProperty("messageId").GetString());
        Assert.Equal("accepted", accepted.RootElement.GetProperty("status").GetString());
        Assert.True(accepted.RootElement.GetProperty("simulated").GetBoolean());

        var message = Assert.Single(await _simulator.CreateClient().GetFromJsonAsync<List<InboxMessage>>("/api/inbox", SimulatorFactory.Json) ?? []);
        Assert.True(message.Simulated);
        Assert.Equal(TestData.MessageId, message.MessageId);
        Assert.Equal(TestData.CaseId, message.CaseId);
        Assert.Equal(TestData.PatientId, message.To);
        Assert.StartsWith("Synthetic ", message.DisplayName, StringComparison.Ordinal);
        Assert.Equal("en", message.Language);
        Assert.Equal(InboxChannel.Sms, message.Channel);
        Assert.Equal(RecipientKind.Patient, message.RecipientKind);
        Assert.Equal(TestData.Now, message.ReceivedAt);
        Assert.Empty(message.Replies);
    }

    [Fact]
    public async Task There_are_only_two_channels_and_the_message_says_which()
    {
        await _simulator.NotifyAsync(messageId: "text", channel: "sms", subject: "ignored on a text message");
        await _simulator.NotifyAsync(messageId: "mail", channel: "EMAIL", to: "SYN-EMAIL-0001", subject: " A follow-up step is waiting for you ");

        var text = await _simulator.InboxMessageAsync("text");
        var mail = await _simulator.InboxMessageAsync("mail");
        Assert.Equal(InboxChannel.Sms, text.Channel);
        Assert.Null(text.Subject);
        Assert.Equal(InboxChannel.Email, mail.Channel);
        Assert.Equal("A follow-up step is waiting for you", mail.Subject);
        Assert.Equal([InboxChannel.Sms, InboxChannel.Email], Enum.GetValues<InboxChannel>());
    }

    [Theory]
    [InlineData("app")]
    [InlineData("portal")]
    [InlineData("push")]
    [InlineData("")]
    public async Task A_message_for_an_app_a_portal_or_any_other_channel_is_refused(string channel)
    {
        var response = await _simulator.NotifyAsync(channel: channel);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty(_simulator.Store.GetInboxMessages());
        Assert.Empty(_simulator.CareLoop.Calls);
    }

    [Fact]
    public async Task Audience_in_the_metadata_files_the_message_for_patient_caregiver_or_staff()
    {
        await _simulator.NotifyAsync(messageId: "patient", audience: "patient");
        await _simulator.NotifyAsync(messageId: "plain", audience: null);
        await _simulator.NotifyAsync(messageId: "caregiver", to: "SYN-CG-EMAIL-0001", channel: "email", audience: "caregiver");
        await _simulator.NotifyAsync(messageId: "staff", to: "SYN-EMAIL-NURSE-NAVIGATORS", channel: "email", audience: "Staff");

        Assert.Equal(RecipientKind.Patient, (await _simulator.InboxMessageAsync("patient")).RecipientKind);
        Assert.Equal(RecipientKind.Patient, (await _simulator.InboxMessageAsync("plain")).RecipientKind);
        var caregiver = await _simulator.InboxMessageAsync("caregiver");
        Assert.Equal(RecipientKind.Caregiver, caregiver.RecipientKind);
        Assert.Equal("Synthetic caregiver SYN-CG-EMAIL-0001", caregiver.DisplayName);
        var staff = await _simulator.InboxMessageAsync("staff");
        Assert.Equal(RecipientKind.Staff, staff.RecipientKind);
        Assert.Equal("Synthetic staff SYN-EMAIL-NURSE-NAVIGATORS", staff.DisplayName);

        await AssertProblemAsync(await _simulator.NotifyAsync(messageId: "other", audience: "public"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Message_body_is_shown_exactly_as_CareLoop_rendered_it()
    {
        const string body = "  SIMULATED\n\tHabari — tafadhali wasiliana na kliniki.  <b>not markup</b> ";

        await _simulator.NotifyAsync(body: body);

        Assert.Equal(body, (await _simulator.InboxMessageAsync()).Body);
    }

    [Fact]
    public async Task Resent_message_does_not_create_a_second_inbox_message_or_a_second_report()
    {
        var first = await _simulator.NotifyAsync();
        var resend = await _simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        Assert.Single(_simulator.Store.GetInboxMessages());
        Assert.Single(_simulator.CareLoop.Callbacks);
    }

    [Theory]
    [InlineData("PT-0001")]
    [InlineData("+971500000000")]
    [InlineData("someone@example.org")]
    [InlineData("SYN-")]
    public async Task Message_to_an_address_that_is_not_synthetic_is_refused_and_not_kept(string to)
    {
        var response = await _simulator.NotifyAsync(to: to);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty(_simulator.Store.GetInboxMessages());
        Assert.Empty(_simulator.CareLoop.Calls);
        var entry = Assert.Single(_simulator.Store.GetLogEntries());
        Assert.Equal(400, entry.StatusCode);
        Assert.Null(entry.Body);
        Assert.Null(entry.MessageId);
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("""{"channel":"sms","to":"SYN-PT-0001","body":"SIMULATED"}""")]
    [InlineData("""{"messageId":"m1","channel":"sms","to":"SYN-PT-0001","body":" "}""")]
    [InlineData("""{"resourceType":"CommunicationRequest","id":"m1","status":"active"}""")]
    public async Task Unusable_body_returns_400_problem_details(string body)
    {
        var response = await _simulator.PostMessageAsync(body);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty(_simulator.Store.GetInboxMessages());
        Assert.Equal(400, Assert.Single(_simulator.Store.GetLogEntries()).StatusCode);
    }

    [Fact]
    public async Task Delivery_report_is_plain_json_that_returns_the_metadata_unchanged()
    {
        const string message = """
            {"messageId":"m1","channel":"email","to":"SYN-EMAIL-0001","subject":"SIMULATED","body":"SIMULATED","language":"en",
             "metadata":{"caseId":"7a1c9e42-3f7b-4c1d-8a55-6e0b1d2c3f44","audience":"patient","anythingElse":{"kept":[1,2,3]}}}
            """;

        await _simulator.PostMessageAsync(message);

        var report = Assert.Single(_simulator.CareLoop.Callbacks);
        Assert.Equal(SimulatorFactory.EmailCallbackUrl, report.Uri.ToString());
        Assert.Equal("application/json; charset=utf-8", report.ContentType);
        Assert.Equal("delivery", report.Type);
        Assert.Equal("delivered", report.Status);
        Assert.Equal("m1", report.Json.GetProperty("messageId").GetString());
        Assert.Equal(TestData.Now, report.Json.GetProperty("at").GetDateTimeOffset());
        Assert.Equal(
            """{"caseId":"7a1c9e42-3f7b-4c1d-8a55-6e0b1d2c3f44","audience":"patient","anythingElse":{"kept":[1,2,3]}}""",
            report.Json.GetProperty("metadata").GetRawText());
        Assert.False(report.Json.TryGetProperty("resourceType", out _));
        Assert.False(report.Json.TryGetProperty("reason", out _));
        Assert.False(report.Json.TryGetProperty("text", out _));
    }

    [Fact]
    public async Task Text_message_reports_go_to_the_sms_callback()
    {
        await _simulator.NotifyAsync(channel: "sms");

        Assert.Equal(SimulatorFactory.SmsCallbackUrl, Assert.Single(_simulator.CareLoop.Callbacks).Uri.ToString());
    }

    [Fact]
    public async Task Exchanges_are_recorded_in_the_message_log()
    {
        await _simulator.NotifyAsync();

        var log = await _simulator.CreateClient().GetFromJsonAsync<List<MessageLogEntry>>("/api/message-log", SimulatorFactory.Json) ?? [];
        var inbound = Assert.Single(log, e => e.Direction == MessageDirection.Inbound);
        Assert.Equal("POST", inbound.Method);
        Assert.Equal("/messages", inbound.Url);
        Assert.Equal("message", inbound.Kind);
        Assert.Equal(TestData.MessageId, inbound.MessageId);
        Assert.Equal(201, inbound.StatusCode);
        Assert.Contains(TestData.MessageId, inbound.Body, StringComparison.Ordinal);
        Assert.True(inbound.Simulated);

        var outbound = Assert.Single(log, e => e.Direction == MessageDirection.Outbound);
        Assert.Equal(SimulatorFactory.SmsCallbackUrl, outbound.Url);
        Assert.Equal("delivery", outbound.Kind);
        Assert.Equal(TestData.MessageId, outbound.MessageId);
        Assert.Equal(200, outbound.StatusCode);
    }

    [Fact]
    public async Task When_the_report_fails_the_message_is_accepted_and_reported_later()
    {
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Deliver, PatientResponseBehaviour.Acknowledge));
        _simulator.CareLoop.AnswerWith(HttpStatusCode.ServiceUnavailable);

        var first = await _simulator.NotifyAsync();

        // CareLoop does not send a patient notification again, so the simulator accepts it and reports later.
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(DeliveryState.Pending, (await _simulator.InboxMessageAsync()).Delivery);
        Assert.Equal(3, _simulator.CareLoop.Callbacks.Count);

        _simulator.CareLoop.AnswerWith(HttpStatusCode.OK);
        Assert.Equal(0, await _simulator.RetryDueAsync());
        _simulator.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(1, await _simulator.RetryDueAsync());

        var message = await _simulator.InboxMessageAsync();
        Assert.Equal(DeliveryState.Delivered, message.Delivery);
        Assert.Equal("1", Assert.Single(message.Replies).Text);
        Assert.Equal(0, await _simulator.RetryDueAsync());
    }

    [Fact]
    public async Task A_resend_finishes_a_delivery_that_is_still_unreported_and_the_retry_then_does_nothing()
    {
        _simulator.CareLoop.AnswerWith(HttpStatusCode.ServiceUnavailable);
        await _simulator.NotifyAsync();
        _simulator.CareLoop.AnswerWith(HttpStatusCode.OK);

        var resend = await _simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        Assert.Equal(DeliveryState.Delivered, (await _simulator.InboxMessageAsync()).Delivery);
        Assert.Single(_simulator.Store.GetInboxMessages());

        var callbacks = _simulator.CareLoop.Callbacks.Count;
        _simulator.Clock.Advance(TimeSpan.FromMinutes(5));
        await _simulator.RetryDueAsync();
        Assert.Equal(callbacks, _simulator.CareLoop.Callbacks.Count);
    }

    [Fact]
    public async Task When_the_callback_throws_the_message_stays_pending_and_the_failure_is_recorded()
    {
        _simulator.CareLoop.Answer = _ => throw new HttpRequestException("SIMULATED: connection refused");

        var response = await _simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(DeliveryState.Pending, _simulator.Store.FindInboxMessage(TestData.MessageId)!.Delivery);
        var reaction = Assert.Single(_simulator.Reactions());
        Assert.False(reaction.Succeeded);
        Assert.Null(reaction.HttpStatusCode);
    }

    [Fact]
    public async Task A_refused_delivery_report_is_retried_with_a_growing_wait_and_then_given_up()
    {
        _simulator.CareLoop.AnswerWith(HttpStatusCode.Unauthorized);

        var response = await _simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(_simulator.CareLoop.Callbacks);

        // The second retry waits longer than the first.
        _simulator.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(1, await _simulator.RetryDueAsync());
        _simulator.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(0, await _simulator.RetryDueAsync());

        for (var i = 0; i < 20; i++)
        {
            _simulator.Clock.Advance(TimeSpan.FromMinutes(2));
            await _simulator.RetryDueAsync();
        }

        // One report while receiving, then the ten background retries, then nothing more.
        Assert.Equal(11, _simulator.CareLoop.Callbacks.Count);
        Assert.Equal(DeliveryState.Pending, (await _simulator.InboxMessageAsync()).Delivery);
        Assert.Equal(HttpStatusCode.Conflict, (await _simulator.ReplyAsync("1")).StatusCode);
    }

    [Fact]
    public async Task The_log_shows_the_status_CareLoop_received_when_the_report_cannot_be_made()
    {
        using var simulator = new SimulatorFactory(secret: string.Empty);

        var response = await simulator.NotifyAsync();

        await AssertProblemAsync(response, HttpStatusCode.BadGateway);
        var inbound = Assert.Single(simulator.Store.GetLogEntries(), e => e.Direction == MessageDirection.Inbound);
        Assert.Equal(502, inbound.StatusCode);
    }

    [Fact]
    public async Task Two_deliveries_of_the_same_message_at_once_report_and_reply_once()
    {
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Deliver, PatientResponseBehaviour.CallMe));
        using var firstReportStarted = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        _simulator.CareLoop.Answer = call =>
        {
            if (call.Type == "delivery")
            {
                firstReportStarted.Set();
                release.Wait(TimeSpan.FromSeconds(10));
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var first = Task.Run(() => _simulator.NotifyAsync());
        Assert.True(firstReportStarted.Wait(TimeSpan.FromSeconds(10)));
        var second = Task.Run(() => _simulator.NotifyAsync());
        await Task.Delay(200);
        release.Set();

        Assert.Equal(HttpStatusCode.Created, (await first).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second).StatusCode);
        Assert.Equal(["delivered", "reply:2"], _simulator.CareLoop.Kinds);
        Assert.Single((await _simulator.InboxMessageAsync()).Replies);
    }

    [Theory]
    [InlineData("caregiver")]
    [InlineData("staff")]
    public async Task A_caregiver_or_staff_message_is_delivered_even_when_the_default_patient_behaviour_fails_delivery(string audience)
    {
        _simulator.Store.SetDefaultBehaviour(new PatientBehaviour(DeliveryBehaviour.Fail, PatientResponseBehaviour.None, "SIMULATED: unreachable"));

        await _simulator.NotifyAsync(to: "SYN-EMAIL-OTHER-0001", channel: "email", audience: audience);

        Assert.Equal(DeliveryState.Delivered, Assert.Single(_simulator.Store.GetInboxMessages()).Delivery);
        Assert.Equal(audience, Assert.Single(_simulator.CareLoop.Callbacks).Json.GetProperty("metadata").GetProperty("audience").GetString());
    }

    [Fact]
    public async Task A_caregiver_message_follows_a_behaviour_set_for_its_own_address()
    {
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Fail, PatientResponseBehaviour.None, "SIMULATED: mailbox full"), "SYN-CG-EMAIL-0001");

        await _simulator.NotifyAsync(to: "SYN-CG-EMAIL-0001", channel: "email", audience: "caregiver");

        Assert.Equal(DeliveryState.Failed, Assert.Single(_simulator.Store.GetInboxMessages()).Delivery);
        var report = Assert.Single(_simulator.CareLoop.Callbacks);
        Assert.Equal("failed", report.Status);
        Assert.Equal("SIMULATED: mailbox full", report.Json.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task An_unknown_path_under_a_host_prefix_is_not_found_and_no_fhir_route_remains()
    {
        var client = _simulator.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/no-such-route")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/messages/123")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/fhir-log")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/case-status/{TestData.CaseId}")).StatusCode);
        var fhir = await client.PostAsync("/fhir/CommunicationRequest", new StringContent("{}", System.Text.Encoding.UTF8, "application/fhir+json"));
        Assert.NotEqual(HttpStatusCode.Created, fhir.StatusCode);
        Assert.False(fhir.IsSuccessStatusCode);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("detail").GetString()));
    }
}
