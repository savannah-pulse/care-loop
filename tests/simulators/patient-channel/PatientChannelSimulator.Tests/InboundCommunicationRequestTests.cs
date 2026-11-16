using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>Inbound CommunicationRequest handling, through the real route (WS8).</summary>
public sealed class InboundCommunicationRequestTests : IDisposable
{
    private readonly SimulatorFactory _simulator = new();

    public void Dispose() => _simulator.Dispose();

    [Fact]
    public async Task CommunicationRequest_lands_in_the_patient_inbox_marked_simulated()
    {
        var response = await _simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(FhirJson.MediaType, response.Content.Headers.ContentType?.MediaType);
        var message = Assert.Single(await _simulator.CreateClient().GetFromJsonAsync<List<InboxMessage>>("/api/inbox", SimulatorFactory.Json) ?? []);
        Assert.True(message.Simulated);
        Assert.Equal(TestData.RequestId, message.MessageId);
        Assert.Equal(TestData.RequestId, message.CommunicationRequestId);
        Assert.Equal(TestData.CaseId, message.CaseTaskId);
        Assert.Equal(TestData.PatientId, message.PatientSyntheticId);
        Assert.StartsWith("Synthetic ", message.PatientDisplayName, StringComparison.Ordinal);
        Assert.Equal("en", message.Language);
        Assert.Equal(RecipientKind.Patient, message.RecipientKind);
        Assert.Equal(TestData.Now, message.ReceivedAt);
    }

    [Fact]
    public async Task Recipient_kind_and_inbox_address_select_the_inbox_channel()
    {
        await _simulator.NotifyAsync(requestId: "app", inboxAddress: "SYN-INBOX-0001");
        await _simulator.NotifyAsync(requestId: "sms", inboxAddress: "SYN-SMS-0001");
        await _simulator.NotifyAsync(requestId: "portal", inboxAddress: "SYN-PORTAL-0001");
        await _simulator.NotifyAsync(requestId: "copy", inboxAddress: "SYN-CG-INBOX-0001", caregiver: true);

        Assert.Equal(InboxChannel.App, (await _simulator.InboxMessageAsync("app")).Channel);
        Assert.Equal(InboxChannel.Sms, (await _simulator.InboxMessageAsync("sms")).Channel);
        Assert.Equal(InboxChannel.Portal, (await _simulator.InboxMessageAsync("portal")).Channel);

        var copy = await _simulator.InboxMessageAsync("caregiver-copy");
        Assert.Equal(InboxChannel.Email, copy.Channel);
        Assert.Equal(RecipientKind.Caregiver, copy.RecipientKind);
        Assert.Equal("SYN-CG-INBOX-0001", copy.PatientSyntheticId);
    }

    [Fact]
    public async Task Message_body_is_shown_exactly_as_CareLoop_rendered_it()
    {
        const string body = "  SIMULATED\n\tHabari — tafadhali wasiliana na kliniki.  <b>not markup</b> ";

        await _simulator.NotifyAsync(body: body);

        Assert.Equal(body, (await _simulator.InboxMessageAsync()).Body);
    }

    [Fact]
    public async Task Resent_CommunicationRequest_does_not_create_a_second_inbox_message()
    {
        var first = await _simulator.NotifyAsync();
        var resend = await _simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        Assert.Single(_simulator.Store.GetInboxMessages());
        Assert.Single(_simulator.CareLoop.Callbacks);
    }

    [Fact]
    public async Task CommunicationRequest_with_non_synthetic_patient_identifier_is_rejected()
    {
        var response = await _simulator.NotifyAsync(patientId: "PT-0001");

        await AssertOperationOutcomeAsync(response, "invalid");
        Assert.Empty(_simulator.Store.GetInboxMessages());
        Assert.Empty(_simulator.CareLoop.Calls);
    }

    [Fact]
    public async Task Caregiver_copy_with_a_non_synthetic_inbox_address_is_rejected()
    {
        var response = await _simulator.NotifyAsync(inboxAddress: "someone@example.org", caregiver: true);

        await AssertOperationOutcomeAsync(response, "invalid");
        Assert.Empty(_simulator.Store.GetInboxMessages());
    }

    [Fact]
    public async Task Unsupported_resource_type_returns_400_OperationOutcome()
    {
        var response = await _simulator.PostFhirAsync("Patient", """{"resourceType":"Patient","id":"SYN-PT-0001"}""");

        await AssertOperationOutcomeAsync(response, "not-supported");
    }

    [Fact]
    public async Task A_different_resource_posted_as_a_CommunicationRequest_returns_400_OperationOutcome()
    {
        var response = await _simulator.PostFhirAsync("CommunicationRequest", """{"resourceType":"Patient","id":"SYN-PT-0001"}""");

        await AssertOperationOutcomeAsync(response, "invalid");
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("{\"resourceType\":\"CommunicationRequest\",\"status\":\"no-such-status\"}")]
    [InlineData("")]
    public async Task Unparseable_body_returns_400_OperationOutcome(string body)
    {
        var response = await _simulator.PostFhirAsync("CommunicationRequest", body);

        await AssertOperationOutcomeAsync(response, "invalid");
        Assert.Empty(_simulator.Store.GetInboxMessages());
    }

    [Fact]
    public async Task Inbound_request_is_recorded_in_the_fhir_message_log()
    {
        await _simulator.NotifyAsync();

        var log = await _simulator.CreateClient().GetFromJsonAsync<List<FhirMessageLogEntry>>("/api/fhir-log", SimulatorFactory.Json) ?? [];

        var inbound = Assert.Single(log, e => e.Direction == FhirMessageDirection.Inbound);
        Assert.Equal("POST", inbound.Method);
        Assert.Equal("/fhir/CommunicationRequest", inbound.Url);
        Assert.Equal("CommunicationRequest", inbound.ResourceType);
        Assert.Equal(TestData.RequestId, inbound.ResourceId);
        Assert.Equal(201, inbound.StatusCode);
        Assert.Contains(TestData.RequestId, inbound.Body, StringComparison.Ordinal);
        Assert.True(inbound.Simulated);

        var outbound = Assert.Single(log, e => e.Direction == FhirMessageDirection.Outbound);
        Assert.Equal(SimulatorFactory.CallbackUrl, outbound.Url);
        Assert.Equal("Communication", outbound.ResourceType);
        Assert.Equal(200, outbound.StatusCode);
    }

    [Fact]
    public async Task A_refused_request_is_logged_with_its_400()
    {
        await _simulator.PostFhirAsync("CommunicationRequest", "this is not json");

        var entry = Assert.Single(_simulator.Store.GetLogEntries());
        Assert.Equal(400, entry.StatusCode);
        Assert.Null(entry.ResourceId);
    }

    [Fact]
    public async Task A_request_refused_as_not_synthetic_is_logged_without_its_body()
    {
        var response = await _simulator.NotifyAsync(patientId: "PT-REAL-LOOKING-0001");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var entry = Assert.Single(_simulator.Store.GetLogEntries());
        Assert.Equal(400, entry.StatusCode);
        Assert.Null(entry.Body);
    }

    [Fact]
    public async Task When_the_report_fails_the_message_is_accepted_and_reported_later()
    {
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Deliver, PatientResponseBehaviour.Acknowledge));
        _simulator.CareLoop.AnswerWith(HttpStatusCode.ServiceUnavailable);

        var first = await _simulator.NotifyAsync();

        // CareLoop does not send a patient notification again, so the channel accepts it and reports later.
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(DeliveryState.Pending, (await _simulator.InboxMessageAsync()).Delivery);
        Assert.Equal(3, _simulator.CareLoop.Callbacks.Count);

        _simulator.CareLoop.AnswerWith(HttpStatusCode.OK);
        Assert.Equal(0, await _simulator.RetryDueAsync());

        _simulator.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(1, await _simulator.RetryDueAsync());

        var message = await _simulator.InboxMessageAsync();
        Assert.Equal(DeliveryState.Delivered, message.Delivery);
        Assert.Equal(ResponseState.Acknowledged, message.Response);
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
        Assert.Equal(DeliveryState.Pending, _simulator.Store.FindInboxMessage(TestData.RequestId)!.Delivery);
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
        Assert.Equal(HttpStatusCode.Conflict, (await _simulator.ActAsync("acknowledge")).StatusCode);
    }

    [Fact]
    public async Task The_log_shows_the_status_CareLoop_received_when_the_report_cannot_be_made()
    {
        using var simulator = new SimulatorFactory(secret: string.Empty);

        var response = await simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var inbound = Assert.Single(simulator.Store.GetLogEntries(), e => e.Direction == FhirMessageDirection.Inbound);
        Assert.Equal(502, inbound.StatusCode);
    }

    [Fact]
    public async Task Two_deliveries_of_the_same_request_at_once_report_and_respond_once()
    {
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Deliver, PatientResponseBehaviour.CallMe));
        using var firstReportStarted = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        _simulator.CareLoop.Answer = call =>
        {
            if (call.Category == "delivery-status")
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
        Assert.Equal(["delivery-status", "call-me"], _simulator.CareLoop.Categories);
        Assert.Equal(1, (await _simulator.InboxMessageAsync()).CallRequestCount);
    }

    [Fact]
    public async Task A_caregiver_copy_is_delivered_even_when_the_default_patient_behaviour_fails_delivery()
    {
        _simulator.Store.SetDefaultBehaviour(new PatientBehaviour(DeliveryBehaviour.Fail, PatientResponseBehaviour.None, "SIMULATED: unreachable"));

        await _simulator.NotifyAsync(requestId: "caregiver-copy", inboxAddress: "SYN-CG-INBOX-0001", caregiver: true);

        Assert.Equal(DeliveryState.Delivered, Assert.Single(_simulator.Store.GetInboxMessages()).Delivery);
    }

    [Fact]
    public async Task A_caregiver_copy_follows_a_behaviour_set_for_its_own_inbox_address()
    {
        _simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Fail, PatientResponseBehaviour.None, "SIMULATED: mailbox full"), "SYN-CG-INBOX-0001");

        await _simulator.NotifyAsync(requestId: "caregiver-copy", inboxAddress: "SYN-CG-INBOX-0001", caregiver: true);

        Assert.Equal(DeliveryState.Failed, Assert.Single(_simulator.Store.GetInboxMessages()).Delivery);
    }

    [Fact]
    public async Task An_unknown_path_under_a_host_prefix_is_not_found()
    {
        var client = _simulator.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/no-such-route")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/fhir/Task/123")).StatusCode);
    }

    private static async Task AssertOperationOutcomeAsync(HttpResponseMessage response, string issueCode, HttpStatusCode status = HttpStatusCode.BadRequest)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(FhirJson.MediaType, response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("OperationOutcome", json.RootElement.GetProperty("resourceType").GetString());
        var issue = json.RootElement.GetProperty("issue")[0];
        Assert.Equal("error", issue.GetProperty("severity").GetString());
        Assert.Equal(issueCode, issue.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(issue.GetProperty("diagnostics").GetString()));
    }
}
