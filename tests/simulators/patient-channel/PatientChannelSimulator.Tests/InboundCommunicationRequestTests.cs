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
    public async Task When_CareLoop_keeps_failing_the_request_fails_and_a_resend_finishes_the_delivery()
    {
        _simulator.CareLoop.AnswerWith(HttpStatusCode.ServiceUnavailable);
        var first = await _simulator.NotifyAsync();

        Assert.Equal(DeliveryState.Pending, (await _simulator.InboxMessageAsync()).Delivery);
        Assert.Equal(3, _simulator.CareLoop.Callbacks.Count);

        // The report never got through, so the request fails and CareLoop's outbox sends it again.
        Assert.Equal(HttpStatusCode.BadGateway, first.StatusCode);

        _simulator.CareLoop.AnswerWith(HttpStatusCode.OK);
        var resend = await _simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        Assert.Equal(DeliveryState.Delivered, (await _simulator.InboxMessageAsync()).Delivery);
        Assert.Single(_simulator.Store.GetInboxMessages());
    }

    [Fact]
    public async Task When_the_callback_throws_the_request_answers_502_so_CareLoop_sends_again()
    {
        _simulator.CareLoop.Answer = _ => throw new HttpRequestException("SIMULATED: connection refused");

        var response = await _simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        await AssertOperationOutcomeAsync(response, "transient", HttpStatusCode.BadGateway);
        Assert.Equal(DeliveryState.Pending, _simulator.Store.FindInboxMessage(TestData.RequestId)!.Delivery);
        var reaction = Assert.Single(_simulator.Reactions());
        Assert.False(reaction.Succeeded);
        Assert.Null(reaction.HttpStatusCode);
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
