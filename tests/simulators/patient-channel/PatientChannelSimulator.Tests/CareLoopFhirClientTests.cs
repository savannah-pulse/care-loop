using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>What the simulator sends to and reads from CareLoop (WS8).</summary>
public sealed class CareLoopFhirClientTests
{
    private const string TaskJson = """
        {
          "resourceType": "Task",
          "id": "7a1c9e42-3f7b-4c1d-8a55-6e0b1d2c3f44",
          "meta": { "lastUpdated": "2026-03-02T08:00:00+00:00" },
          "status": "in-progress",
          "intent": "order",
          "businessStatus": { "coding": [ { "system": "https://careloop.invalid/fhir/CodeSystem/case-stage", "code": "follow-up-active" } ] },
          "statusReason": { "text": "SIMULATED: awaiting contact" },
          "owner": { "reference": "Practitioner/SYN-STAFF-0001", "display": "Synthetic Coordinator" },
          "lastModified": "2026-03-02T09:00:00+00:00",
          "restriction": { "period": { "end": "2026-03-04T09:00:00+00:00" } }
        }
        """;

    [Fact]
    public async Task Callback_is_signed_over_the_exact_bytes_sent()
    {
        using var simulator = new SimulatorFactory();

        await simulator.NotifyAsync();

        var call = Assert.Single(simulator.CareLoop.Callbacks);
        var expected = "sha256=" + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(SimulatorFactory.Secret), call.Body));
        Assert.Equal(expected, call.Signature);
        Assert.Equal(SimulatorFactory.CallbackUrl, call.Uri.ToString());
        Assert.Equal("application/fhir+json; charset=utf-8", call.ContentType);

        // No byte-order mark: CareLoop hashes the body as text, so a BOM would break the signature.
        Assert.Equal((byte)'{', call.Body[0]);
    }

    [Fact]
    public void Signature_matches_the_scheme_CareLoop_documents()
    {
        // HMAC-SHA256 test vector from RFC 4231, test case 2.
        var signature = CareLoopFhirClient.SignatureFor(Encoding.UTF8.GetBytes("what do ya want for nothing?"), "Jefe");

        Assert.Equal("sha256=5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843", signature);
    }

    [Fact]
    public async Task Callback_carries_what_CareLoop_requires()
    {
        using var simulator = new SimulatorFactory();

        await simulator.NotifyAsync();

        var json = Assert.Single(simulator.CareLoop.Callbacks).Json;
        Assert.Equal("Communication", json.GetProperty("resourceType").GetString());
        Assert.Equal($"{TestData.RequestId}-delivery", json.GetProperty("id").GetString());
        Assert.Equal("1", json.GetProperty("meta").GetProperty("versionId").GetString());
        Assert.Equal($"Task/{TestData.CaseId}", json.GetProperty("about")[0].GetProperty("reference").GetString());
        Assert.Equal($"CommunicationRequest/{TestData.RequestId}", json.GetProperty("basedOn")[0].GetProperty("reference").GetString());
        Assert.Equal(TestData.Now, DateTimeOffset.Parse(json.GetProperty("sent").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
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
        Assert.Equal(DeliveryState.Pending, simulator.Store.FindInboxMessage(TestData.RequestId)!.Delivery);
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
    public async Task Case_status_shows_coordination_fields_only()
    {
        using var simulator = new SimulatorFactory();
        simulator.CareLoop.Answer = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(TaskJson, Encoding.UTF8, FhirJson.MediaType),
        };

        var response = await simulator.CreateClient().GetAsync($"/api/case-status/{TestData.CaseId}");
        var text = await response.Content.ReadAsStringAsync();
        var status = await response.Content.ReadFromJsonAsync<PatientCaseStatus>(SimulatorFactory.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestData.CaseId, status!.TaskId);
        Assert.Equal("in-progress", status.TaskStatus);
        Assert.Equal("follow-up-active", status.Stage);
        Assert.Equal("SIMULATED: awaiting contact", status.StatusReason);
        Assert.Equal(new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero), status.DueBy);
        Assert.Equal(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero), status.LastUpdated);
        Assert.True(status.Simulated);
        Assert.DoesNotContain("Coordinator", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SYN-STAFF", text, StringComparison.Ordinal);

        var call = Assert.Single(simulator.CareLoop.Calls);
        Assert.Equal(HttpMethod.Get, call.Method);
        Assert.Equal($"http://careloop.invalid/fhir/Task/{TestData.CaseId}", call.Uri.ToString());
        Assert.Null(call.Signature);
    }

    [Fact]
    public async Task Case_status_is_404_when_CareLoop_has_no_such_task()
    {
        using var simulator = new SimulatorFactory();

        var response = await simulator.CreateClient().GetAsync("/api/case-status/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Case_status_is_502_when_CareLoop_fails_or_answers_with_something_else()
    {
        using var simulator = new SimulatorFactory();
        var client = simulator.CreateClient();

        simulator.CareLoop.AnswerWith(HttpStatusCode.InternalServerError);
        var failed = await client.GetAsync($"/api/case-status/{TestData.CaseId}");

        simulator.CareLoop.Answer = _ => throw new HttpRequestException("SIMULATED: connection refused");
        var unreachable = await client.GetAsync($"/api/case-status/{TestData.CaseId}");

        simulator.CareLoop.Answer = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"resourceType":"Patient","id":"SYN-PT-0001"}""") };
        var notATask = await client.GetAsync($"/api/case-status/{TestData.CaseId}");

        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, unreachable.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, notATask.StatusCode);
    }

    [Fact]
    public async Task Reads_and_callbacks_are_listed_in_the_log_newest_first()
    {
        using var simulator = new SimulatorFactory();
        await simulator.NotifyAsync();
        simulator.Clock.Advance(TimeSpan.FromMinutes(1));
        await simulator.CreateClient().GetAsync("/api/case-status/unknown");

        var log = await simulator.CreateClient().GetFromJsonAsync<List<FhirMessageLogEntry>>("/api/fhir-log", SimulatorFactory.Json);

        Assert.Equal(3, log!.Count);
        Assert.Equal(("GET", "Task", 404), (log[0].Method, log[0].ResourceType, log[0].StatusCode));
        Assert.All(log, e => Assert.True(e.Simulated));
    }

    [Fact]
    public async Task OpenApi_document_is_generated()
    {
        using var simulator = new SimulatorFactory();

        var response = await simulator.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/inbox", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
