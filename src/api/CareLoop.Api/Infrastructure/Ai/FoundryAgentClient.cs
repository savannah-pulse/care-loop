using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CareLoop.Api.Features.Agents;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Calls the Microsoft Foundry agents of one project over their OpenAI Responses endpoint.
/// </summary>
/// <remarks>
/// <para>
/// Each call is <c>POST {ProjectEndpoint}/agents/{agentName}/endpoint/protocols/openai/responses?api-version=v1</c>
/// with an Entra bearer token (<see cref="AgentAccessToken"/>) and the body
/// <c>{"input":[{"role":"user","content":"&lt;prompt&gt;"}]}</c>. The answer is OpenAI Responses JSON: the text is
/// <c>output_text</c> when present, otherwise every <c>output[].content[].text</c> joined in order.
/// </para>
/// <para>
/// It fails closed. A missing token, any status other than success (401, 403, 429, 5xx and the rest), a timeout
/// (<see cref="AgentOptions.TimeoutSeconds"/>, token included), an answer marked incomplete or failed, a refusal and an
/// empty or unreadable answer all raise <see cref="AgentUnavailableException"/>. A half-written answer is never
/// returned.
/// </para>
/// <para>
/// It logs the agent's name, the status and how long the call took, and on a failure the agent's error code and
/// message. It never logs the prompt or the text, which describe cases.
/// </para>
/// </remarks>
public sealed class FoundryAgentClient : IAgentClient
{
    private const int MaxLoggedErrorLength = 500;

    private readonly HttpClient _http;
    private readonly AgentAccessToken _token;
    private readonly IOptions<AgentOptions> _options;
    private readonly ILogger<FoundryAgentClient> _logger;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="http">HTTP client for the Foundry project; its timeout is set from <see cref="AgentOptions.TimeoutSeconds"/> when registered.</param>
    /// <param name="token">Supplies the bearer token.</param>
    /// <param name="options">The project endpoint and the timeout.</param>
    /// <param name="logger">Records each call's agent, status and duration, never its prompt.</param>
    public FoundryAgentClient(HttpClient http, AgentAccessToken token, IOptions<AgentOptions> options, ILogger<FoundryAgentClient> logger)
    {
        _http = http;
        _token = token;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> AskAsync(string agentName, string prompt, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        var options = _options.Value;
        var started = Stopwatch.GetTimestamp();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            var token = await _token.GetAsync(agentName, timeout.Token);
            using var request = new HttpRequestMessage(HttpMethod.Post, ResponsesUri(options.ProjectEndpoint, agentName))
            {
                Content = JsonContent.Create(new ResponsesRequest([new ResponsesInput("user", prompt)])),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _http.SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Agent {Agent} answered {Status} in {ElapsedMs} ms: {AgentError}",
                    agentName, status, Elapsed(started), ErrorOf(body));
                throw new AgentUnavailableException(agentName, $"The agent answered HTTP {status}.", status);
            }

            var text = TextOfLogged(body, agentName, status, started);
            _logger.LogInformation("Agent {Agent} answered {Status} in {ElapsedMs} ms", agentName, status, Elapsed(started));
            return text;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Agent {Agent} did not answer within {TimeoutSeconds} s", agentName, options.TimeoutSeconds);
            var seconds = options.TimeoutSeconds == 1 ? "1 second" : $"{options.TimeoutSeconds} seconds";
            throw new AgentUnavailableException(agentName, $"The agent did not answer within {seconds}.", innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Agent {Agent} could not be reached after {ElapsedMs} ms: {Error}", agentName, Elapsed(started), ex.Message);
            throw new AgentUnavailableException(agentName, "The agent could not be reached.", innerException: ex);
        }
        catch (AgentUnavailableException ex) when (ex.StatusCode is null)
        {
            _logger.LogWarning("Agent {Agent} was not called after {ElapsedMs} ms: {Reason}", agentName, Elapsed(started), ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Builds the address of an agent's Responses endpoint.
    /// </summary>
    /// <param name="projectEndpoint">The Foundry project endpoint.</param>
    /// <param name="agentName">The agent's name.</param>
    /// <returns>The absolute address, with the agent name escaped as one path segment.</returns>
    /// <exception cref="InvalidOperationException">No project endpoint is configured.</exception>
    public static Uri ResponsesUri(string? projectEndpoint, string agentName)
    {
        if (string.IsNullOrWhiteSpace(projectEndpoint))
        {
            throw new InvalidOperationException("Ai:Agents:ProjectEndpoint is not configured.");
        }

        return new Uri($"{projectEndpoint.Trim().TrimEnd('/')}/agents/{Uri.EscapeDataString(agentName)}/endpoint/protocols/openai/responses?api-version=v1");
    }

    /// <summary>
    /// Reads the text out of an OpenAI Responses answer.
    /// </summary>
    /// <param name="body">The response body.</param>
    /// <param name="agentName">The agent that answered, named in a failure.</param>
    /// <param name="status">The HTTP status, kept on a failure.</param>
    /// <returns><c>output_text</c> when present, otherwise every <c>output[].content[].text</c> joined by new lines; trimmed.</returns>
    /// <exception cref="AgentUnavailableException">The answer is not JSON, is marked other than completed, holds a refusal or has no text.</exception>
    public static string TextOf(string body, string agentName, int status)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new AgentUnavailableException(agentName, "The agent's answer could not be read.", status);
            }

            if (root.TryGetProperty("status", out var state) && state.ValueKind == JsonValueKind.String
                && !string.Equals(state.GetString(), "completed", StringComparison.OrdinalIgnoreCase))
            {
                throw new AgentUnavailableException(agentName, $"The agent's answer was {state.GetString()}, not completed.", status);
            }

            var text = root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String
                ? outputText.GetString()
                : JoinOutput(root, agentName, status);
            return string.IsNullOrWhiteSpace(text)
                ? throw new AgentUnavailableException(agentName, "The agent wrote no text.", status)
                : text.Trim();
        }
        catch (JsonException ex)
        {
            throw new AgentUnavailableException(agentName, "The agent's answer could not be read.", status, ex);
        }
    }

    private string TextOfLogged(string body, string agentName, int status, long started)
    {
        try
        {
            return TextOf(body, agentName, status);
        }
        catch (AgentUnavailableException ex)
        {
            _logger.LogWarning("Agent {Agent} answered {Status} in {ElapsedMs} ms with no usable text: {Reason}",
                agentName, status, Elapsed(started), ex.Message);
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

    private sealed record ResponsesRequest(IReadOnlyList<ResponsesInput> Input);

    private sealed record ResponsesInput(string Role, string Content);
}
