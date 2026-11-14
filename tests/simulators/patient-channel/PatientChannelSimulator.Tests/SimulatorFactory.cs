using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PatientChannelSimulator.Inbound;
using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>A clock the test moves by hand.</summary>
internal sealed class FakeClock : ISimulatorClock
{
    public DateTimeOffset UtcNow { get; set; } = TestData.Now;

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>One call the simulator made to CareLoop.</summary>
internal sealed record CareLoopCall(HttpMethod Method, Uri Uri, byte[] Body, string? Signature, string? ContentType)
{
    public string Text => Encoding.UTF8.GetString(Body);

    public JsonElement Json => JsonDocument.Parse(Body).RootElement;

    /// <summary>The Communication category code, or null for a call that is not a Communication.</summary>
    public string? Category =>
        Method == HttpMethod.Post
        && Json.TryGetProperty("category", out var categories)
            ? categories[0].GetProperty("coding")[0].GetProperty("code").GetString()
            : null;

    public string? Status => Method == HttpMethod.Post ? Json.GetProperty("status").GetString() : null;
}

/// <summary>Stands in for CareLoop: records every call and answers as the test says.</summary>
internal sealed class StubCareLoop : HttpMessageHandler
{
    private readonly List<CareLoopCall> _calls = [];

    /// <summary>Decides the answer to a call; the default accepts every callback and knows no Task.</summary>
    public Func<CareLoopCall, HttpResponseMessage> Answer { get; set; } =
        call => new HttpResponseMessage(call.Method == HttpMethod.Post ? HttpStatusCode.OK : HttpStatusCode.NotFound);

    public IReadOnlyList<CareLoopCall> Calls
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    public IReadOnlyList<CareLoopCall> Callbacks => [.. Calls.Where(c => c.Method == HttpMethod.Post)];

    public IReadOnlyList<string?> Categories => [.. Callbacks.Select(c => c.Category)];

    public void AnswerWith(HttpStatusCode status) => Answer = _ => new HttpResponseMessage(status);

    /// <summary>Refuses callbacks of one category with a status code and accepts the rest.</summary>
    public void Refuse(string category, HttpStatusCode status) =>
        Answer = call => new HttpResponseMessage(call.Category == category ? status : HttpStatusCode.OK);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var call = new CareLoopCall(
            request.Method,
            request.RequestUri!,
            body,
            request.Headers.TryGetValues(CareLoopFhirClient.SignatureHeader, out var values) ? values.Single() : null,
            request.Content?.Headers.ContentType?.ToString());
        lock (_calls)
        {
            _calls.Add(call);
        }

        return Answer(call);
    }
}

/// <summary>
/// Hosts the real simulator in memory with CareLoop stubbed out, a hand-moved clock and no
/// background dispatcher (tests send due responses themselves, so nothing depends on timing).
/// </summary>
internal sealed class SimulatorFactory : WebApplicationFactory<Program>
{
    public const string Secret = "test-secret-not-a-credential";
    public const string CallbackUrl = "http://careloop.invalid/notifications/inbound/simulator";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _secret;

    public SimulatorFactory(string secret = Secret)
    {
        _secret = secret;
    }

    public FakeClock Clock { get; } = new();

    public StubCareLoop CareLoop { get; } = new();

    public SimulatorStore Store => Services.GetRequiredService<SimulatorStore>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("CareLoop:FhirBaseUrl", "http://careloop.invalid/fhir");
        builder.UseSetting("CareLoop:NotificationCallbackUrl", CallbackUrl);
        builder.UseSetting("CareLoop:InboundSecret", _secret);
        builder.UseSetting("CareLoop:RetryDelayMilliseconds", "0");
        builder.UseSetting("Scenarios:Directory", ScenariosDirectory());

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISimulatorClock>();
            services.AddSingleton<ISimulatorClock>(Clock);
            services.AddHttpClient<CareLoopFhirClient>().ConfigurePrimaryHttpMessageHandler(() => CareLoop);

            var dispatcher = services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ResponseDispatcher));
            services.Remove(dispatcher);
        });
    }

    /// <summary>Posts a notification request the way CareLoop's simulator provider does.</summary>
    public Task<HttpResponseMessage> NotifyAsync(
        string requestId = TestData.RequestId,
        string patientId = TestData.PatientId,
        string inboxAddress = "SYN-INBOX-0001",
        bool caregiver = false,
        string body = "SIMULATED: please contact the clinic about your follow-up.") =>
        PostFhirAsync("CommunicationRequest", FhirJson.Serialize(Request(requestId, patientId, inboxAddress, caregiver, body)));

    public Task<HttpResponseMessage> PostFhirAsync(string resourceType, string json) =>
        CreateClient().PostAsync($"/fhir/{resourceType}", new StringContent(json, Encoding.UTF8, FhirJson.MediaType));

    public async Task<InboxMessage> InboxMessageAsync(string messageId = TestData.RequestId) =>
        (await CreateClient().GetFromJsonAsync<InboxMessage>($"/api/inbox/{messageId}", Json))!;

    public Task<HttpResponseMessage> ActAsync(string action, string messageId = TestData.RequestId) =>
        CreateClient().PostAsync($"/api/inbox/{messageId}/{action}", null);

    public void SetBehaviour(PatientBehaviour behaviour, string patientId = TestData.PatientId) =>
        Store.SetBehaviour(new PatientBehaviourAssignment(patientId, behaviour, null));

    /// <summary>Sends the responses that are due on the fake clock, as the dispatcher would.</summary>
    public async Task<int> SendDueAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AutomaticResponder>().SendDueAsync(CancellationToken.None);
    }

    public IReadOnlyList<PatientReactionRecord> Reactions(string patientId = TestData.PatientId) => Store.GetReactions([patientId]);

    /// <summary>The CommunicationRequest CareLoop sends: same fields as its simulator notification provider.</summary>
    public static CommunicationRequest Request(string requestId, string patientId, string inboxAddress, bool caregiver, string body) =>
        new()
        {
            Id = caregiver ? "caregiver-" + requestId : requestId,
            Identifier = [new Identifier("https://careloop.invalid/fhir/sid/notification-attempt", requestId)],
            Status = RequestStatus.Active,
            Category = [new CodeableConcept("https://careloop.invalid/fhir/CodeSystem/recipient-kind", caregiver ? "caregiver" : "patient")],
            Subject = caregiver ? null : new ResourceReference($"Patient/{patientId}"),
            Recipient = [new ResourceReference { Identifier = new Identifier("https://careloop.invalid/fhir/sid/simulator-inbox", inboxAddress) }],
            About = [new ResourceReference($"Task/{TestData.CaseId}")],
            Payload = [new CommunicationRequest.PayloadComponent { Content = new FhirString(body) }],
            Language = "en",
        };

    public static string ScenariosDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "simulators", "scenarios");
            if (File.Exists(Path.Combine(candidate, "scenario.schema.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not find src/simulators/scenarios above " + AppContext.BaseDirectory);
    }
}
