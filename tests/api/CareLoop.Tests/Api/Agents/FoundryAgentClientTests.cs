using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using CareLoop.Api.Features.Agents;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Tests.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api.Agents;

/// <summary>
/// The Foundry agent client asks the pinned agent version through the project's Responses endpoint with a bearer
/// token, reads the text back, fails closed on every kind of failure (another agent version and an oversized request
/// included), and logs the agent version, status, duration and correlation identifier but never the prompt.
/// </summary>
public sealed class FoundryAgentClientTests
{
    private const string Endpoint = "https://syn-foundry.invalid/api/projects/syn";
    private const string Prompt = "SYN prompt: Case 3F6B1C2A, overdue.";

    private static readonly AgentReference Agent = new("role-briefing", "2");

    [Fact]
    public async Task Asks_the_pinned_agent_version_through_the_projects_responses_endpoint_with_a_bearer_token()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"status":"completed","output_text":"  SYN briefing.  ","agent_reference":{"type":"agent_reference","name":"role-briefing","version":"2"}}"""));
        var request = Request();

        var answer = await Client(handler).AskAsync(request, CancellationToken.None);

        Assert.Equal(new AgentText("SYN briefing.", Agent), answer);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal($"{Endpoint}/openai/v1/responses", sent.Uri);
        Assert.Equal("Bearer SYN-IDENTITY-TOKEN", sent.Authorization);
        Assert.Equal(request.CorrelationId.ToString(), sent.ClientRequestId);
        using var body = JsonDocument.Parse(sent.Body);
        var reference = body.RootElement.GetProperty("agent_reference");
        Assert.Equal(("agent_reference", "role-briefing", "2"),
            (reference.GetProperty("type").GetString(), reference.GetProperty("name").GetString(), reference.GetProperty("version").GetString()));
        var input = Assert.Single(body.RootElement.GetProperty("input").EnumerateArray());
        Assert.Equal("user", input.GetProperty("role").GetString());
        Assert.Equal(Prompt, input.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Without_output_text_the_message_parts_are_joined_in_order()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """
            {"status":"completed","output":[
              {"type":"reasoning","summary":[]},
              {"type":"message","role":"assistant","content":[{"type":"output_text","text":"SYN first."},{"type":"output_text","text":"SYN second."}]}
            ]}
            """));

        Assert.Equal("SYN first.\nSYN second.", (await Client(handler).AskAsync(Request(), CancellationToken.None)).Text);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Any_unsuccessful_status_is_one_unavailable_failure(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => Json(status, """{"error":{"code":"PermissionDenied","message":"SYN principal lacks a data action"}}"""));

        var failure = await Assert.ThrowsAsync<AgentUnavailableException>(() => Client(handler).AskAsync(Request(), CancellationToken.None));

        Assert.Equal((int)status, failure.StatusCode);
        Assert.Equal("role-briefing", failure.AgentName);
    }

    [Theory]
    [InlineData("""{"status":"completed","output_text":"   "}""")]
    [InlineData("""{"status":"completed","output":[]}""")]
    [InlineData("""{"status":"incomplete","output_text":"SYN half a sente"}""")]
    [InlineData("""{"status":"completed","output":[{"type":"message","content":[{"type":"refusal","refusal":"SYN no"}]}]}""")]
    [InlineData("""{"status":"completed","output_text":"SYN from another version","agent_reference":{"type":"agent_reference","name":"role-briefing","version":"3"}}""")]
    [InlineData("""{"status":"completed","output_text":"SYN from another agent","agent_reference":{"type":"agent_reference","name":"impact-summary","version":"2"}}""")]
    [InlineData("not json")]
    public async Task An_empty_incomplete_refused_unreadable_or_other_version_answer_is_unavailable_never_partial_text(string body)
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, body));

        await Assert.ThrowsAsync<AgentUnavailableException>(() => Client(handler).AskAsync(Request(), CancellationToken.None));
    }

    [Fact]
    public async Task A_request_over_the_size_limit_is_refused_before_any_token_or_call()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"output_text":"SYN"}"""));
        var credential = new CountingCredential();

        var failure = await Assert.ThrowsAsync<AgentUnavailableException>(() =>
            Client(handler, Settings(maxRequestBytes: 200), credential: credential).AskAsync(Request(new string('x', 300)), CancellationToken.None));

        Assert.Null(failure.StatusCode);
        Assert.Contains("larger than the configured limit", failure.Message);
        Assert.Empty(handler.Requests);
        Assert.Equal(0, credential.Calls);
    }

    [Fact]
    public async Task A_slow_agent_times_out_as_unavailable()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Json(HttpStatusCode.OK, """{"output_text":"SYN too late"}""");
        });

        var failure = await Assert.ThrowsAsync<AgentUnavailableException>(() =>
            Client(handler, Settings(timeoutSeconds: 1)).AskAsync(Request(), CancellationToken.None));

        Assert.Null(failure.StatusCode);
        Assert.Contains("within 1 second.", failure.Message);
    }

    [Fact]
    public async Task No_token_for_the_apis_identity_is_unavailable_and_nothing_is_sent()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"output_text":"SYN"}"""));
        var token = new AgentAccessToken(new FailingCredential(), Options.Create(new AgentOptions()), new HostEnvironment(Environments.Production),
            new FakeClock(), NullLogger<AgentAccessToken>.Instance);

        await Assert.ThrowsAsync<AgentUnavailableException>(() => Client(handler, token: token).AskAsync(Request(), CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_log_names_the_agent_version_status_duration_and_request_and_never_the_prompt()
    {
        var logs = new CapturingLogger();
        var ok = new StubHandler(_ => Json(HttpStatusCode.OK, """{"output_text":"SYN text the log must not hold"}"""));
        var refused = new StubHandler(_ => Json(HttpStatusCode.Forbidden, """{"error":{"code":"PermissionDenied","message":"SYN no role"}}"""));
        var foundryRefused = new StubHandler(_ => Json(HttpStatusCode.Forbidden, """{"code":"UserError","message":"SYN identity lacks agents/write"}"""));
        var request = Request();

        await Client(ok, logger: logs).AskAsync(request, CancellationToken.None);
        await Assert.ThrowsAsync<AgentUnavailableException>(() => Client(refused, logger: logs).AskAsync(request, CancellationToken.None));
        await Assert.ThrowsAsync<AgentUnavailableException>(() => Client(foundryRefused, logger: logs).AskAsync(request, CancellationToken.None));

        Assert.Equal(3, logs.Messages.Count);
        Assert.All(logs.Messages, m => Assert.Contains("role-briefing:2", m));
        Assert.All(logs.Messages, m => Assert.Contains(request.CorrelationId.ToString(), m));
        Assert.Contains(logs.Messages, m => m.Contains("200", StringComparison.Ordinal));
        Assert.Contains(logs.Messages, m => m.Contains("403", StringComparison.Ordinal) && m.Contains("PermissionDenied: SYN no role", StringComparison.Ordinal));
        Assert.Contains(logs.Messages, m => m.Contains("UserError: SYN identity lacks agents/write", StringComparison.Ordinal));
        Assert.All(logs.Messages, m => Assert.DoesNotContain("3F6B1C2A", m));
        Assert.All(logs.Messages, m => Assert.DoesNotContain("must not hold", m));
    }

    [Fact]
    public async Task A_development_token_is_used_only_in_Development()
    {
        var credential = new CountingCredential();
        var options = Options.Create(new AgentOptions { AccessToken = " SYN-DEV-TOKEN " });

        var development = new AgentAccessToken(credential, options, new HostEnvironment(Environments.Development), new FakeClock(), NullLogger<AgentAccessToken>.Instance);
        var production = new AgentAccessToken(credential, options, new HostEnvironment(Environments.Production), new FakeClock(), NullLogger<AgentAccessToken>.Instance);

        Assert.Equal("SYN-DEV-TOKEN", await development.GetAsync("role-briefing", CancellationToken.None));
        Assert.Equal(0, credential.Calls);
        Assert.Equal("SYN-IDENTITY-TOKEN", await production.GetAsync("role-briefing", CancellationToken.None));
        Assert.Equal("SYN-IDENTITY-TOKEN", await production.GetAsync("role-briefing", CancellationToken.None));
        Assert.Equal(1, credential.Calls);
        Assert.Equal([AgentAccessToken.Scope], credential.Scopes);
    }

    [Theory]
    [InlineData(false, "NoOp", null, null, "role-briefing", 30, 32768, true)]
    [InlineData(false, "FoundryDirect", null, null, "", 0, 0, true)]
    [InlineData(true, "NoOp", null, null, "role-briefing:2", 30, 32768, true)]
    [InlineData(true, "FoundryDirect", Endpoint, null, "role-briefing:2", 30, 32768, true)]
    [InlineData(true, "FoundryDirect", null, null, "role-briefing:2", 30, 32768, false)]
    [InlineData(true, "FoundryDirect", "http://syn-foundry.invalid/api/projects/syn", null, "role-briefing:2", 30, 32768, false)]
    [InlineData(true, "FoundryDirect", Endpoint, null, "role-briefing", 30, 32768, false)]
    [InlineData(true, "FoundryDirect", Endpoint, null, "role-briefing:2/../x", 30, 32768, false)]
    [InlineData(true, "FoundryDirect", Endpoint, null, "", 30, 32768, false)]
    [InlineData(true, "FoundryDirect", Endpoint, null, "role-briefing:2", 0, 32768, false)]
    [InlineData(true, "FoundryDirect", Endpoint, null, "role-briefing:2", 301, 32768, false)]
    [InlineData(true, "FoundryDirect", Endpoint, null, "role-briefing:2", 30, 0, false)]
    [InlineData(true, "AiWorkflow", null, "http://ai-workflow:8080", "role-briefing:2", 30, 32768, true)]
    [InlineData(true, "AiWorkflow", Endpoint, null, "role-briefing:2", 30, 32768, false)]
    public void Settings_are_checked_only_once_the_agents_are_switched_on(bool enabled, string provider, string? projectEndpoint,
        string? workflowUrl, string agent, int timeout, int maxBytes, bool valid)
    {
        var ai = new AiOptions
        {
            Provider = provider, TimeoutSeconds = timeout, MaxRequestBytes = maxBytes,
            FoundryDirect = new FoundryOptions { ProjectEndpoint = projectEndpoint }, AiWorkflow = new AiWorkflowOptions { BaseUrl = workflowUrl },
        };

        Assert.Equal(valid, AgentOptions.IsValid(new AgentOptions { Enabled = enabled, RoleBriefing = agent }, ai));
    }

    [Fact]
    public void Base_settings_keep_the_agents_off_with_each_agent_pinned_and_no_endpoint_or_token()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json")).Build();
        var options = new AgentOptions();
        configuration.GetSection(AgentOptions.SectionName).Bind(options);
        var ai = new AiOptions();
        configuration.GetSection(AiOptions.SectionName).Bind(ai);

        Assert.False(options.Enabled);
        Assert.Equal(AiRoute.NoOp, ai.Route());
        Assert.True(string.IsNullOrEmpty(ai.FoundryDirect.ProjectEndpoint));
        Assert.Null(options.AccessToken);
        Assert.Equal((new AgentReference("role-briefing", "2"), new AgentReference("impact-summary", "4"), new AgentReference("patient-notification", "2")),
            (options.RoleBriefingAgent(), options.ImpactSummaryAgent(), options.PatientNotificationAgent()));
        Assert.Equal(["PLACEHOLDER-URGENCY-B"], options.UrgentCodes);
        Assert.Null(configuration["Ai:Agents:ProjectEndpoint"]);
        Assert.Null(configuration["Ai:Agents:TimeoutSeconds"]);
    }

    private static AgentRequest Request(string prompt = Prompt) => new()
    {
        Purpose = "role_briefing", Agent = Agent, PromptVersion = "role-briefing-v2", Prompt = prompt, CorrelationId = Guid.NewGuid(),
    };

    private static AiOptions Settings(int timeoutSeconds = 30, int maxRequestBytes = 32_768) => new()
    {
        Provider = "FoundryDirect", TimeoutSeconds = timeoutSeconds, MaxRequestBytes = maxRequestBytes,
        FoundryDirect = new FoundryOptions { ProjectEndpoint = Endpoint },
    };

    private static FoundryAgentClient Client(StubHandler handler, AiOptions? options = null, AgentAccessToken? token = null,
        ILogger<FoundryAgentClient>? logger = null, CountingCredential? credential = null)
    {
        token ??= new AgentAccessToken(credential ?? new CountingCredential(), Options.Create(new AgentOptions()),
            new HostEnvironment(Environments.Production), new FakeClock(), NullLogger<AgentAccessToken>.Instance);
        return new FoundryAgentClient(new HttpClient(handler), token, Options.Create(options ?? Settings()), logger ?? NullLogger<FoundryAgentClient>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record SentRequest(HttpMethod Method, string Uri, string? Authorization, string? ClientRequestId, string Body);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _answer;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer)
            : this((request, _) => Task.FromResult(answer(request)))
        {
        }

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) => _answer = answer;

        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new SentRequest(request.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues(FoundryAgentClient.ClientRequestIdHeader, out var ids) ? ids.Single() : null,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return await _answer(request, cancellationToken);
        }
    }

    private sealed class CountingCredential : TokenCredential
    {
        public int Calls { get; private set; }

        public string[] Scopes { get; private set; } = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Calls++;
            Scopes = requestContext.Scopes;
            return ValueTask.FromResult(new AccessToken("SYN-IDENTITY-TOKEN", DateTimeOffset.UtcNow.AddHours(1)));
        }
    }

    private sealed class FailingCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new CredentialUnavailableException("SYN no identity here");

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new CredentialUnavailableException("SYN no identity here");
    }

    private sealed class HostEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "CareLoop.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CapturingLogger : ILogger<FoundryAgentClient>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
