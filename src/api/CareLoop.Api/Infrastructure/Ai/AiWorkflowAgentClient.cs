using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Api.Features.Agents;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Hands an agent request to the Python AI workflow service, which calls the Foundry agent: the <c>AiWorkflow</c> route
/// of <c>Ai:Provider</c>.
/// </summary>
/// <remarks>
/// <para>
/// A thin client for a <b>proposed</b> contract, pending the team's decision on where the agents are called from
/// (<c>docs/ai-design/integration.md</c>, "The route"). It posts the purpose-scoped request to
/// <c>{Ai:AiWorkflow:BaseUrl}/agents/drafts</c>: <c>request_id</c> (the correlation identifier), <c>purpose</c>,
/// <c>agent</c> (name and pinned version), <c>prompt_version</c>, <c>language</c> and the <c>prompt</c> the API built.
/// The service calls exactly that agent version and answers <c>200</c> with the same <c>request_id</c>, <c>purpose</c>,
/// <c>agent</c> and <c>prompt_version</c>, <c>status</c> <c>draft</c> and the <c>text</c>, or <c>422</c> with a
/// <c>reason</c>. The service does not build the prompt: the API stays the data boundary.
/// </para>
/// <para>
/// The service is trusted to call a model, not to be correct. A request over <c>Ai:MaxRequestBytes</c>, any status
/// other than <c>200</c> (the <c>422</c> refusal included), a timeout (<c>Ai:TimeoutSeconds</c>), an unreadable answer,
/// an answer to another request, purpose, prompt version or agent version, a status other than <c>draft</c> and an empty
/// text all raise <see cref="AgentUnavailableException"/>, so the endpoint answers the same calm 503 as on the direct
/// route. It logs the agent version, status, duration and correlation identifier, never the prompt or the text.
/// </para>
/// </remarks>
public sealed class AiWorkflowAgentClient : IAgentClient
{
    /// <summary>The service's proposed path for the dashboard agents, under <c>Ai:AiWorkflow:BaseUrl</c>.</summary>
    public const string Path = "agents/drafts";

    private const int MaxLoggedReasonLength = 300;

    private readonly HttpClient _http;
    private readonly IOptions<AiOptions> _options;
    private readonly ILogger<AiWorkflowAgentClient> _logger;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="http">HTTP client for the Python service; its timeout is set from <c>Ai:TimeoutSeconds</c> when registered.</param>
    /// <param name="options">The service's address, the timeout and the request-size limit.</param>
    /// <param name="logger">Records each call's agent version, status, duration and correlation identifier, never its prompt.</param>
    public AiWorkflowAgentClient(HttpClient http, IOptions<AiOptions> options, ILogger<AiWorkflowAgentClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Route => nameof(AiRoute.AiWorkflow);

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
            var body = new DraftRequest(request.CorrelationId.ToString(), request.Purpose, new AgentBody(agent.Name, agent.Version),
                request.PromptVersion, "en", request.Prompt);
            using var content = AgentRequestBody.Create(body, request, options.MaxRequestBytes);
            using var response = await _http.PostAsync(DraftsUri(options.AiWorkflow.BaseUrl), content, timeout.Token);
            var status = (int)response.StatusCode;
            if (status != StatusCodes.Status200OK)
            {
                var reason = await ReasonOfAsync(response, timeout.Token);
                _logger.LogWarning("AI workflow service answered {Status} for agent {Agent} in {ElapsedMs} ms (request {CorrelationId}): {Reason}",
                    status, agent, Elapsed(started), request.CorrelationId, reason);
                throw new AgentUnavailableException(agent.Name,
                    status == StatusCodes.Status422UnprocessableEntity ? "The AI service refused the request." : $"The AI service answered HTTP {status}.", status);
            }

            var answer = await ReadAsync(response, request, status, timeout.Token);
            _logger.LogInformation("AI workflow service answered {Status} for agent {Agent} in {ElapsedMs} ms (request {CorrelationId})",
                status, agent, Elapsed(started), request.CorrelationId);
            return answer;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("AI workflow service did not answer for agent {Agent} within {TimeoutSeconds} s (request {CorrelationId})",
                agent, seconds, request.CorrelationId);
            throw new AgentUnavailableException(agent.Name, $"The AI service did not answer within {(seconds == 1 ? "1 second" : $"{seconds} seconds")}.", innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("AI workflow service could not be reached for agent {Agent} after {ElapsedMs} ms (request {CorrelationId}): {Error}",
                agent, Elapsed(started), request.CorrelationId, ex.Message);
            throw new AgentUnavailableException(agent.Name, "The AI service could not be reached.", innerException: ex);
        }
        catch (AgentUnavailableException ex) when (ex.StatusCode is null)
        {
            _logger.LogWarning("AI workflow service was not called for agent {Agent} after {ElapsedMs} ms (request {CorrelationId}): {Reason}",
                agent, Elapsed(started), request.CorrelationId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Builds the address of the service's proposed endpoint for the dashboard agents.
    /// </summary>
    /// <param name="baseUrl">The service's address, <c>Ai:AiWorkflow:BaseUrl</c>.</param>
    /// <returns><c>{baseUrl}/agents/drafts</c>.</returns>
    /// <exception cref="InvalidOperationException">No address is configured.</exception>
    public static Uri DraftsUri(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Ai:AiWorkflow:BaseUrl is not configured.");
        }

        return new Uri($"{baseUrl.Trim().TrimEnd('/')}/{Path}");
    }

    private async Task<AgentText> ReadAsync(HttpResponseMessage response, AgentRequest request, int status, CancellationToken ct)
    {
        var agent = request.Agent;
        DraftAnswer? answer;
        try
        {
            answer = await response.Content.ReadFromJsonAsync<DraftAnswer>(ct);
        }
        catch (JsonException ex)
        {
            throw Unusable(request, status, "The AI service's answer could not be read.", ex);
        }

        if (answer is null
            || answer.RequestId != request.CorrelationId.ToString()
            || answer.Purpose != request.Purpose
            || answer.PromptVersion != request.PromptVersion
            || !string.Equals(answer.Agent?.Name, agent.Name, StringComparison.OrdinalIgnoreCase)
            || answer.Agent?.Version != agent.Version)
        {
            throw Unusable(request, status, "The AI service answered another request, prompt or agent version.");
        }

        if (answer.Status != "draft")
        {
            throw Unusable(request, status, $"The AI service's answer was {answer.Status ?? "unmarked"}, not a draft.");
        }

        return string.IsNullOrWhiteSpace(answer.Text)
            ? throw Unusable(request, status, "The agent wrote no text.")
            : new AgentText(answer.Text.Trim(), agent);
    }

    private AgentUnavailableException Unusable(AgentRequest request, int status, string reason, Exception? inner = null)
    {
        _logger.LogWarning("AI workflow service answered {Status} for agent {Agent} (request {CorrelationId}) with no usable text: {Reason}",
            status, request.Agent, request.CorrelationId, reason);
        return new AgentUnavailableException(request.Agent.Name, reason, status, inner);
    }

    // The service's own reason, shortened, for the log only; it never reaches the screen.
    private static async Task<string> ReasonOfAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String)
            {
                body = reason.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // Not JSON: log the start of the body as it is.
        }

        return body.Length <= MaxLoggedReasonLength ? body : string.Concat(body.AsSpan(0, MaxLoggedReasonLength), "…");
    }

    private static long Elapsed(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private sealed record AgentBody(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("version")] string? Version);

    private sealed record DraftRequest(
        [property: JsonPropertyName("request_id")] string RequestId,
        [property: JsonPropertyName("purpose")] string Purpose,
        [property: JsonPropertyName("agent")] AgentBody Agent,
        [property: JsonPropertyName("prompt_version")] string PromptVersion,
        [property: JsonPropertyName("language")] string Language,
        [property: JsonPropertyName("prompt")] string Prompt);

    private sealed record DraftAnswer(
        [property: JsonPropertyName("request_id")] string? RequestId,
        [property: JsonPropertyName("purpose")] string? Purpose,
        [property: JsonPropertyName("agent")] AgentBody? Agent,
        [property: JsonPropertyName("prompt_version")] string? PromptVersion,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("text")] string? Text);
}
