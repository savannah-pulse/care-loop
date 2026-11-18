using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Api.Features.Agents;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Calls a pinned version of a Microsoft Foundry agent from the API, over the project's OpenAI Responses endpoint: the
/// <c>FoundryDirect</c> route of <c>Ai:Provider</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each call is <c>POST {Ai:FoundryDirect:ProjectEndpoint}/openai/v1/responses</c> with an Entra bearer token
/// (<see cref="AgentAccessToken"/>), the header <c>x-ms-client-request-id</c> carrying the call's correlation identifier,
/// and the body <c>{"agent_reference":{"type":"agent_reference","name":"…","version":"…"},"input":[{"role":"user","content":"…"}]}</c>.
/// Naming the version in <c>agent_reference</c> is how Foundry's Responses API addresses one agent version (the request
/// the Agent Framework's <c>FoundryAgent(agent_name, agent_version)</c> sends, as in <c>src/ai_agents/*/run_agent.py</c>);
/// the agent's own endpoint serves whichever version the project's version selector picks, which a portal edit can
/// change. The answer is OpenAI Responses JSON: the text is <c>output_text</c> when present, otherwise every
/// <c>output[].content[].text</c> joined in order, and <c>agent_reference</c> names the version that answered.
/// </para>
/// <para>
/// It fails closed. A request over <c>Ai:MaxRequestBytes</c>, a missing token, any status other than success (401,
/// 403, 429, 5xx and the rest), a timeout (<c>Ai:TimeoutSeconds</c>, token included), an answer marked incomplete or
/// failed, a refusal, an empty or unreadable answer and an answer from another agent version all raise
/// <see cref="AgentUnavailableException"/>. A half-written answer is never returned.
/// </para>
/// <para>
/// It logs the agent version, the status, how long the call took and the correlation identifier, and on a failure the
/// agent's error code and message. It never logs the prompt or the text, which describe cases.
/// </para>
/// </remarks>
public sealed class FoundryAgentClient : IAgentClient
{
    /// <summary>The header Azure services read a caller's request identifier from, and log with the request.</summary>
    public const string ClientRequestIdHeader = "x-ms-client-request-id";

    private const int MaxLoggedErrorLength = 500;

    private readonly HttpClient _http;
    private readonly AgentAccessToken _token;
    private readonly IOptions<AiOptions> _options;
    private readonly ILogger<FoundryAgentClient> _logger;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="http">HTTP client for the Foundry project; its timeout is set from <c>Ai:TimeoutSeconds</c> when registered.</param>
    /// <param name="token">Supplies the bearer token.</param>
    /// <param name="options">The project endpoint, the timeout and the request-size limit.</param>
    /// <param name="logger">Records each call's agent version, status, duration and correlation identifier, never its prompt.</param>
    public FoundryAgentClient(HttpClient http, AgentAccessToken token, IOptions<AiOptions> options, ILogger<FoundryAgentClient> logger)
    {
        _http = http;
        _token = token;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Route => nameof(AiRoute.FoundryDirect);

    /// <inheritdoc/>
    public bool IsEnabled => true;

    /// <inheritdoc/>
    public async Task<AgentText> AskAsync(AgentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Prompt);
        var options = _options.Value;
        var agent = request.Agent;
        var started = Stopwatch.GetTimestamp();
        var seconds = Math.Clamp(options.TimeoutSeconds, 1, AiOptions.MaximumTimeoutSeconds);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        try
        {
            using var content = AgentRequestBody.Create(
                new ResponsesRequest(new AgentReferenceBody(agent.Name, agent.Version), [new ResponsesInput("user", request.Prompt)]),
                request, options.MaxRequestBytes);
            var token = await _token.GetAsync(agent.Name, timeout.Token);
            using var message = new HttpRequestMessage(HttpMethod.Post, ResponsesUri(options.FoundryDirect.ProjectEndpoint)) { Content = content };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            message.Headers.TryAddWithoutValidation(ClientRequestIdHeader, request.CorrelationId.ToString());
            using var response = await _http.SendAsync(message, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Agent {Agent} answered {Status} in {ElapsedMs} ms (request {CorrelationId}): {AgentError}",
                    agent, status, Elapsed(started), request.CorrelationId, ErrorOf(body));
                throw new AgentUnavailableException(agent.Name, $"The agent answered HTTP {status}.", status);
            }

            var text = ReadLogged(body, agent, status, started, request.CorrelationId);
            _logger.LogInformation("Agent {Agent} answered {Status} in {ElapsedMs} ms (request {CorrelationId})",
                agent, status, Elapsed(started), request.CorrelationId);
            return text;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Agent {Agent} did not answer within {TimeoutSeconds} s (request {CorrelationId})", agent, seconds, request.CorrelationId);
            throw new AgentUnavailableException(agent.Name, $"The agent did not answer within {(seconds == 1 ? "1 second" : $"{seconds} seconds")}.", innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Agent {Agent} could not be reached after {ElapsedMs} ms (request {CorrelationId}): {Error}",
                agent, Elapsed(started), request.CorrelationId, ex.Message);
            throw new AgentUnavailableException(agent.Name, "The agent could not be reached.", innerException: ex);
        }
        catch (AgentUnavailableException ex) when (ex.StatusCode is null)
        {
            _logger.LogWarning("Agent {Agent} was not called after {ElapsedMs} ms (request {CorrelationId}): {Reason}",
                agent, Elapsed(started), request.CorrelationId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Builds the address of the project's Responses endpoint.
    /// </summary>
    /// <param name="projectEndpoint">The Foundry project endpoint, <c>Ai:FoundryDirect:ProjectEndpoint</c>.</param>
    /// <returns><c>{projectEndpoint}/openai/v1/responses</c>.</returns>
    /// <exception cref="InvalidOperationException">No project endpoint is configured.</exception>
    public static Uri ResponsesUri(string? projectEndpoint)
    {
        if (string.IsNullOrWhiteSpace(projectEndpoint))
        {
            throw new InvalidOperationException("Ai:FoundryDirect:ProjectEndpoint is not configured.");
        }

        return new Uri($"{projectEndpoint.Trim().TrimEnd('/')}/openai/v1/responses");
    }

    /// <summary>
    /// Reads the text out of an OpenAI Responses answer, and checks which agent version wrote it.
    /// </summary>
    /// <param name="body">The response body.</param>
    /// <param name="requested">The agent version that was asked, named in a failure.</param>
    /// <param name="status">The HTTP status, kept on a failure.</param>
    /// <returns>
    /// <c>output_text</c> when present, otherwise every <c>output[].content[].text</c> joined by new lines, trimmed; with
    /// the requested agent version, which the answer's <c>agent_reference</c> confirmed when it named one.
    /// </returns>
    /// <exception cref="AgentUnavailableException">The answer is not JSON, is marked other than completed, holds a refusal,
    /// has no text, or names another agent version than the one pinned.</exception>
    public static AgentText Read(string body, AgentReference requested, int status)
    {
        ArgumentNullException.ThrowIfNull(requested);
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new AgentUnavailableException(requested.Name, "The agent's answer could not be read.", status);
            }

            if (root.TryGetProperty("status", out var state) && state.ValueKind == JsonValueKind.String
                && !string.Equals(state.GetString(), "completed", StringComparison.OrdinalIgnoreCase))
            {
                throw new AgentUnavailableException(requested.Name, $"The agent's answer was {state.GetString()}, not completed.", status);
            }

            CheckVersion(root, requested, status);
            var text = root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String
                ? outputText.GetString()
                : JoinOutput(root, requested.Name, status);
            return string.IsNullOrWhiteSpace(text)
                ? throw new AgentUnavailableException(requested.Name, "The agent wrote no text.", status)
                : new AgentText(text.Trim(), requested);
        }
        catch (JsonException ex)
        {
            throw new AgentUnavailableException(requested.Name, "The agent's answer could not be read.", status, ex);
        }
    }

    // The answer names the agent version that wrote it. It must be the pinned one: the audit already recorded it.
    private static void CheckVersion(JsonElement root, AgentReference requested, int status)
    {
        if (!root.TryGetProperty("agent_reference", out var answered) || answered.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var name = answered.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        var version = answered.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        if (name is not null && !string.Equals(name, requested.Name, StringComparison.OrdinalIgnoreCase)
            || version is not null && !string.Equals(version, requested.Version, StringComparison.Ordinal))
        {
            throw new AgentUnavailableException(requested.Name,
                $"The answer came from {name ?? requested.Name} version {version ?? "unknown"}, not the pinned version {requested.Version}.", status);
        }
    }

    private AgentText ReadLogged(string body, AgentReference agent, int status, long started, Guid correlationId)
    {
        try
        {
            return Read(body, agent, status);
        }
        catch (AgentUnavailableException ex)
        {
            _logger.LogWarning("Agent {Agent} answered {Status} in {ElapsedMs} ms (request {CorrelationId}) with no usable text: {Reason}",
                agent, status, Elapsed(started), correlationId, ex.Message);
            throw;
        }
    }

    private static string JoinOutput(JsonElement root, string agentName, int status)
    {
        var text = new StringBuilder();
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (part.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "refusal")
                {
                    throw new AgentUnavailableException(agentName, "The agent refused to write this.", status);
                }

                if (part.TryGetProperty("text", out var value) && value.ValueKind == JsonValueKind.String)
                {
                    if (text.Length > 0)
                    {
                        text.Append('\n');
                    }

                    text.Append(value.GetString());
                }
            }
        }

        return text.ToString();
    }

    /// <summary>The agent's own error code and message, shortened, for the log; never anything from the prompt.</summary>
    /// <remarks>Foundry answers either <c>{"error":{"code","message"}}</c> or <c>{"code","message"}</c> at the top level.</remarks>
    private static string ErrorOf(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                var error = root.TryGetProperty("error", out var nested) && nested.ValueKind == JsonValueKind.Object ? nested : root;
                var code = error.TryGetProperty("code", out var c) ? c.ToString() : null;
                var message = error.TryGetProperty("message", out var m) ? m.ToString() : null;
                if (code is not null || message is not null)
                {
                    return Shorten($"{code}: {message}");
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON: fall through and log the start of the body, which comes from the agent, not from the prompt.
        }

        return Shorten(body);
    }

    private static string Shorten(string text) =>
        text.Length <= MaxLoggedErrorLength ? text : string.Concat(text.AsSpan(0, MaxLoggedErrorLength), "…");

    private static long Elapsed(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private sealed record ResponsesRequest(
        [property: JsonPropertyName("agent_reference")] AgentReferenceBody AgentReference,
        [property: JsonPropertyName("input")] IReadOnlyList<ResponsesInput> Input);

    private sealed record AgentReferenceBody(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("version")] string Version)
    {
        [JsonPropertyName("type")]
        public string Type => "agent_reference";
    }

    private sealed record ResponsesInput(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);
}
