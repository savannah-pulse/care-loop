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
/// The Foundry agent client sends the Responses request with a bearer token, reads the text back, fails closed on every
/// kind of failure, and logs the agent, status and duration but never the prompt.
/// </summary>
public sealed class FoundryAgentClientTests
{
    private const string Endpoint = "https://syn-foundry.invalid/api/projects/syn";
    private const string Prompt = "SYN prompt: Case 3F6B1C2A, overdue.";

    [Fact]
    public async Task Posts_the_prompt_to_the_agents_responses_endpoint_with_a_bearer_token_and_returns_output_text()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"status":"completed","output_text":"  SYN briefing.  "}"""));

        var text = await Client(handler).AskAsync("role-briefing", Prompt, CancellationToken.None);

        Assert.Equal("SYN briefing.", text);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"{Endpoint}/agents/role-briefing/endpoint/protocols/openai/responses?api-version=v1", request.Uri);
        Assert.Equal("Bearer SYN-IDENTITY-TOKEN", request.Authorization);
        using var body = JsonDocument.Parse(request.Body);
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

        Assert.Equal("SYN first.\nSYN second.", await Client(handler).AskAsync("impact-summary", Prompt, CancellationToken.None));
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

        var failure = await Assert.ThrowsAsync<AgentUnavailableException>(() => Client(handler).AskAsync("role-briefing", Prompt, CancellationToken.None));

        Assert.Equal((int)status, failure.StatusCode);
        Assert.Equal("role-briefing", failure.AgentName);
    }

    [Theory]
    [InlineData("""{"status":"completed","output_text":"   "}""")]
    [InlineData("""{"status":"completed","output":[]}""")]
    [InlineData("""{"status":"incomplete","output_text":"SYN half a sente"}""")]
    [InlineData("""{"status":"completed","output":[{"type":"message","content":[{"type":"refusal","refusal":"SYN no"}]}]}""")]
    [InlineData("not json")]
    public async Task An_empty_incomplete_refused_or_unreadable_answer_is_unavailable_never_partial_text(string body)
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, body));

        await Assert.ThrowsAsync<AgentUnavailableException>(() => Client(handler).AskAsync("patient-notification", Prompt, CancellationToken.None));
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
            Client(handler, new AgentOptions { Enabled = true, ProjectEndpoint = Endpoint, TimeoutSeconds = 1 })
                .AskAsync("role-briefing", Prompt, CancellationToken.None));

        Assert.Null(failure.StatusCode);
        Assert.Contains("within 1 second.", failure.Message);
    }

    [Fact]
    public async Task No_token_for_the_apis_identity_is_unavailable_and_nothing_is_sent()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"output_text":"SYN"}"""));
        var token = new AgentAccessToken(new FailingCredential(), Options.Create(new AgentOptions()), new HostEnvironment(Environments.Production),
            new FakeClock(), NullLogger<AgentAccessToken>.Instance);

        await Assert.ThrowsAsync<AgentUnavailableException>(() => Client(handler, token: token).AskAsync("role-briefing", Prompt, CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_log_names_the_agent_status_and_duration_and_never_the_prompt()
    {
        var logs = new CapturingLogger();
        var ok = new StubHandler(_ => Json(HttpStatusCode.OK, """{"output_text":"SYN text the log must not hold"}"""));
        var refused = new StubHandler(_ => Json(HttpStatusCode.Forbidden, """{"error":{"code":"PermissionDenied","message":"SYN no role"}}"""));
        var foundryRefused = new StubHandler(_ => Json(HttpStatusCode.Forbidden, """{"code":"UserError","message":"SYN identity lacks agents/write"}"""));

        await Client(ok, logger: logs).AskAsync("role-briefing", Prompt, CancellationToken.None);
        await Assert.ThrowsAsync<AgentUnavailableException>(() => Client(refused, logger: logs).AskAsync("role-briefing", Prompt, CancellationToken.None));
        await Assert.ThrowsAsync<AgentUnavailableException>(() => Client(foundryRefused, logger: logs).AskAsync("role-briefing", Prompt, CancellationToken.None));

        Assert.Equal(3, logs.Messages.Count);
        Assert.All(logs.Messages, m => Assert.Contains("role-briefing", m));
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
    [InlineData(false, null, "role-briefing", 30, true)]
    [InlineData(true, Endpoint, "role-briefing", 30, true)]
    [InlineData(true, null, "role-briefing", 30, false)]
    [InlineData(true, "http://syn-foundry.invalid/api/projects/syn", "role-briefing", 30, false)]
    [InlineData(true, Endpoint, "role-briefing/../x", 30, false)]
    [InlineData(true, Endpoint, "", 30, false)]
    [InlineData(true, Endpoint, "role-briefing", 0, false)]
    [InlineData(true, Endpoint, "role-briefing", 301, false)]
    public void Settings_are_checked_only_once_the_agents_are_switched_on(bool enabled, string? endpoint, string agent, int timeout, bool valid) =>
        Assert.Equal(valid, AgentOptions.IsValid(new AgentOptions { Enabled = enabled, ProjectEndpoint = endpoint, RoleBriefing = agent, TimeoutSeconds = timeout }));

    [Fact]
    public void Base_settings_keep_the_agents_off_with_no_endpoint_or_token()
    {
        var options = new AgentOptions();
        new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json")).Build()
            .GetSection(AgentOptions.SectionName).Bind(options);

        Assert.False(options.Enabled);
        Assert.True(string.IsNullOrEmpty(options.ProjectEndpoint));
        Assert.Null(options.AccessToken);
        Assert.Equal(("role-briefing", "impact-summary", "patient-notification", 30),
            (options.RoleBriefing, options.ImpactSummary, options.PatientNotification, options.TimeoutSeconds));
        Assert.Equal(["PLACEHOLDER-URGENCY-B"], options.UrgentCodes);
    }

    private static FoundryAgentClient Client(StubHandler handler, AgentOptions? options = null, AgentAccessToken? token = null,
        ILogger<FoundryAgentClient>? logger = null)
    {
        var settings = Options.Create(options ?? new AgentOptions { Enabled = true, ProjectEndpoint = Endpoint });
        token ??= new AgentAccessToken(new CountingCredential(), settings, new HostEnvironment(Environments.Production), new FakeClock(), NullLogger<AgentAccessToken>.Instance);
        return new FoundryAgentClient(new HttpClient(handler), token, settings, logger ?? NullLogger<FoundryAgentClient>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record SentRequest(HttpMethod Method, string Uri, string? Authorization, string Body);

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
