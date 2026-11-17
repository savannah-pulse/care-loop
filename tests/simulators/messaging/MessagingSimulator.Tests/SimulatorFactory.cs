using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MessagingSimulator.Inbound;
using MessagingSimulator.Outbound;
using MessagingSimulator.Scenarios;
using MessagingSimulator.State;

namespace MessagingSimulator.Tests;

/// <summary>A clock the test moves by hand.</summary>
internal sealed class FakeClock : ISimulatorClock
{
    public DateTimeOffset UtcNow { get; set; } = TestData.Now;

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>One call the simulator made to CareLoop: a delivery report or a reply, as JSON.</summary>
internal sealed record CareLoopCall(HttpMethod Method, Uri Uri, byte[] Body, string? Signature, string? ContentType)
{
    public string Text => Encoding.UTF8.GetString(Body);

    public JsonElement Json => JsonDocument.Parse(Body).RootElement;

    /// <summary><c>delivery</c> or <c>reply</c>.</summary>
    public string? Type => String("type");

    /// <summary>The status of a delivery report: <c>delivered</c> or <c>failed</c>.</summary>
    public string? Status => String("status");

    /// <summary>The text of a reply, exactly as sent.</summary>
    public string? ReplyText => String("text");

    /// <summary>A short name for the callback: <c>delivered</c>, <c>failed</c>, or <c>reply:</c> and the text.</summary>
    public string Kind => Type == "reply" ? "reply:" + ReplyText : Status ?? "?";

    private string? String(string name) =>
        Json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>Stands in for CareLoop: records every call and answers as the test says.</summary>
internal sealed class StubCareLoop : HttpMessageHandler
{
    private readonly List<CareLoopCall> _calls = [];

    /// <summary>Decides the answer to a call; the default accepts every callback.</summary>
    public Func<CareLoopCall, HttpResponseMessage> Answer { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

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

    public IReadOnlyList<CareLoopCall> Callbacks => Calls;

    public IReadOnlyList<string> Kinds => [.. Calls.Select(c => c.Kind)];

    public void AnswerWith(HttpStatusCode status) => Answer = _ => new HttpResponseMessage(status);

    /// <summary>Refuses replies with a status code and accepts delivery reports.</summary>
    public void RefuseReplies(HttpStatusCode status) =>
        Answer = call => new HttpResponseMessage(call.Type == "reply" ? status : HttpStatusCode.OK);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var call = new CareLoopCall(
            request.Method,
            request.RequestUri!,
            body,
            request.Headers.TryGetValues(CareLoopCallbackClient.SignatureHeader, out var values) ? values.Single() : null,
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
    public const string CallbackBaseUrl = "http://careloop.invalid/notifications/inbound";
    public const string SmsCallbackUrl = CallbackBaseUrl + "/sms";
    public const string EmailCallbackUrl = CallbackBaseUrl + "/email";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _secret;
    private readonly string _scenariosDirectory;

    public SimulatorFactory(string secret = Secret, string? scenariosDirectory = null)
    {
        _secret = secret;
        _scenariosDirectory = scenariosDirectory ?? ScenariosDirectory();
    }

    public FakeClock Clock { get; } = new();

    public StubCareLoop CareLoop { get; } = new();

    public SimulatorStore Store => Services.GetRequiredService<SimulatorStore>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("CareLoop:CallbackBaseUrl", CallbackBaseUrl);
        builder.UseSetting("CareLoop:InboundSecret", _secret);
        builder.UseSetting("CareLoop:RetryDelayMilliseconds", "0");
        builder.UseSetting("Scenarios:Directory", _scenariosDirectory);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISimulatorClock>();
            services.AddSingleton<ISimulatorClock>(Clock);
            services.AddHttpClient<CareLoopCallbackClient>().ConfigurePrimaryHttpMessageHandler(() => CareLoop);

            var dispatcher = services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ResponseDispatcher));
            services.Remove(dispatcher);
        });
    }

    /// <summary>Posts a message the way CareLoop's text-message and email providers do.</summary>
    public Task<HttpResponseMessage> NotifyAsync(
        string messageId = TestData.MessageId,
        string to = TestData.PatientId,
        string channel = "sms",
        string? audience = "patient",
        string body = TestData.Body,
        string? subject = null,
        string? caseId = TestData.CaseId) =>
        PostMessageAsync(JsonSerializer.Serialize(Message(messageId, to, channel, audience, body, subject, caseId), Json));

    public Task<HttpResponseMessage> PostMessageAsync(string json) =>
        CreateClient().PostAsync("/messages", new StringContent(json, Encoding.UTF8, "application/json"));

    public async Task<InboxMessage> InboxMessageAsync(string messageId = TestData.MessageId) =>
        (await CreateClient().GetFromJsonAsync<InboxMessage>($"/api/inbox/{messageId}", Json))!;

    /// <summary>Replies to a message as the simulated patient typing in the ClientApp.</summary>
    public Task<HttpResponseMessage> ReplyAsync(string? text, string messageId = TestData.MessageId) =>
        CreateClient().PostAsJsonAsync($"/api/inbox/{messageId}/reply", new { text });

    public void SetBehaviour(PatientBehaviour behaviour, string patientId = TestData.PatientId) =>
        Store.SetBehaviour(new PatientBehaviourAssignment(patientId, behaviour, null));

    /// <summary>Sends the responses that are due on the fake clock, as the dispatcher would.</summary>
    public async Task<int> SendDueAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AutomaticResponder>().SendDueAsync(CancellationToken.None);
    }

    /// <summary>Tries the delivery reports that are due on the fake clock, as the dispatcher would.</summary>
    public async Task<int> RetryDueAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DeliveryReportRetrier>().RetryDueAsync(CancellationToken.None);
    }

    public IReadOnlyList<PatientReactionRecord> Reactions(string patientId = TestData.PatientId) => Store.GetReactions([patientId]);

    /// <summary>The message CareLoop sends: same fields as its text-message and email providers.</summary>
    public static object Message(string messageId, string to, string channel, string? audience, string body, string? subject, string? caseId) =>
        new
        {
            messageId,
            channel,
            to,
            subject,
            body,
            language = "en",
            metadata = new { caseId, audience },
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
