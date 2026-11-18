using System.Net;
using System.Text;
using System.Text.Json;
using CareLoop.Api.Features.Agents;
using CareLoop.Api.Infrastructure;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Domain.Time;
using CareLoop.Tests.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api.Agents;

/// <summary>
/// The shared <c>Ai:Provider</c> chooses how the agents are reached, and the <c>AiWorkflow</c> route hands the same
/// purpose-scoped request to the Python service, failing closed on anything but a matching draft.
/// </summary>
public sealed class AgentRouteTests
{
    private const string BaseUrl = "http://syn-ai-workflow.invalid:8080";

    private static readonly AgentReference Agent = new("impact-summary", "4");

    [Theory]
    [InlineData(null, typeof(NoOpAgentClient), false)]
    [InlineData("NoOp", typeof(NoOpAgentClient), false)]
    [InlineData("foundrydirect", typeof(FoundryAgentClient), true)]
    [InlineData("AiWorkflow", typeof(AiWorkflowAgentClient), true)]
    public void Ai_Provider_chooses_the_agent_client(string? provider, Type expected, bool enabled)
    {
        using var services = Services(new Dictionary<string, string?> { ["Ai:Provider"] = provider });

        var client = services.GetRequiredService<IAgentClient>();

        Assert.IsType(expected, client);
        Assert.Equal(enabled, client.IsEnabled);
    }

    [Fact]
    public void Switched_on_without_the_routes_address_the_settings_are_refused()
    {
        using var services = Services(new Dictionary<string, string?> { ["Ai:Provider"] = "FoundryDirect", ["Ai:Agents:Enabled"] = "true" });

        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<AgentOptions>>().Value);
    }

    [Fact]
    public async Task The_AiWorkflow_route_posts_the_purpose_scoped_request_and_returns_the_draft()
    {
        var request = Request();
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, Answer(request)));

        var answer = await Client(handler).AskAsync(request, CancellationToken.None);

        Assert.Equal(new AgentText("SYN words.", Agent), answer);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal($"{BaseUrl}/agents/drafts", sent.Uri);
        using var body = JsonDocument.Parse(sent.Body);
        var root = body.RootElement;
        Assert.Equal(request.CorrelationId.ToString(), root.GetProperty("request_id").GetString());
        Assert.Equal("impact_summary", root.GetProperty("purpose").GetString());
        Assert.Equal(("impact-summary", "4"), (root.GetProperty("agent").GetProperty("name").GetString(), root.GetProperty("agent").GetProperty("version").GetString()));
        Assert.Equal("impact-summary-v2", root.GetProperty("prompt_version").GetString());
        Assert.Equal("en", root.GetProperty("language").GetString());
        Assert.Equal("SYN prompt", root.GetProperty("prompt").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, null)]
    [InlineData(HttpStatusCode.InternalServerError, null)]
    [InlineData(HttpStatusCode.OK, "request_id")]
    [InlineData(HttpStatusCode.OK, "purpose")]
    [InlineData(HttpStatusCode.OK, "prompt_version")]
    [InlineData(HttpStatusCode.OK, "version")]
    [InlineData(HttpStatusCode.OK, "status")]
    [InlineData(HttpStatusCode.OK, "text")]
    public async Task On_the_AiWorkflow_route_a_refusal_or_an_answer_that_does_not_match_is_unavailable(HttpStatusCode status, string? wrong)
    {
        var request = Request();
        var answer = Answer(request, wrong);
        var handler = new StubHandler(_ => Json(status, status == HttpStatusCode.OK ? answer : """{"request_id":"x","reason":"SYN refused"}"""));

        var failure = await Assert.ThrowsAsync<AgentUnavailableException>(() => Client(handler).AskAsync(request, CancellationToken.None));

        Assert.Equal((int)status, failure.StatusCode);
    }

    [Fact]
    public async Task On_the_AiWorkflow_route_a_request_over_the_size_limit_is_never_sent()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsAsync<AgentUnavailableException>(() =>
            Client(handler, maxRequestBytes: 100).AskAsync(Request(new string('x', 200)), CancellationToken.None));

        Assert.Empty(handler.Requests);
    }

    private static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IClock>(new FakeClock());
        services.AddSingleton<IHostEnvironment>(new HostEnvironment());
        services.AddSummaries(configuration).AddAiAgents(configuration);
        return services.BuildServiceProvider();
    }

    private static AgentRequest Request(string prompt = "SYN prompt") => new()
    {
        Purpose = "impact_summary", Agent = Agent, PromptVersion = "impact-summary-v2", Prompt = prompt, CorrelationId = Guid.NewGuid(),
    };

    private static string Answer(AgentRequest request, string? wrong = null) => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["request_id"] = wrong == "request_id" ? Guid.NewGuid().ToString() : request.CorrelationId.ToString(),
        ["purpose"] = wrong == "purpose" ? "role_briefing" : request.Purpose,
        ["agent"] = new Dictionary<string, string> { ["name"] = Agent.Name, ["version"] = wrong == "version" ? "5" : Agent.Version },
        ["prompt_version"] = wrong == "prompt_version" ? "impact-summary-v1" : request.PromptVersion,
        ["status"] = wrong == "status" ? "approved" : "draft",
        ["text"] = wrong == "text" ? "  " : " SYN words. ",
    });

    private static AiWorkflowAgentClient Client(StubHandler handler, int maxRequestBytes = 32_768) =>
        new(new HttpClient(handler), Options.Create(new AiOptions
        {
            Provider = "AiWorkflow", MaxRequestBytes = maxRequestBytes, AiWorkflow = new AiWorkflowOptions { BaseUrl = BaseUrl },
        }), NullLogger<AiWorkflowAgentClient>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record SentRequest(string Uri, string Body);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new SentRequest(request.RequestUri!.ToString(),
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return answer(request);
        }
    }

    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "CareLoop.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
