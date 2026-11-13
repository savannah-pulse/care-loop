using CareLoop.Api.Features.Summaries;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Drafts handoff summaries by calling the Python AI workflow service, which owns the prompt and calls Foundry.
/// </summary>
/// <remarks>
/// The agreed route (decision 8). The prompt, the response schema and the Foundry credentials all
/// live in one service, and the API holds no model credential at all. Will <c>POST</c> the fact
/// set to <c>{BaseUrl}/summaries/handoff</c> and read back a draft.
/// <para>
/// The service is trusted to call a model, not to be correct: whatever comes back is validated
/// here and again in <see cref="HandoffSummaryDrafter"/> before anything is stored. A non-success
/// status, a timeout or a body that does not match the schema raises
/// <see cref="AiDraftUnavailableException"/>; nothing is ever substituted for a draft.
/// </para>
/// </remarks>
public sealed class AiWorkflowProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly IOptions<AiOptions> _options;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="http">HTTP client used to call the Python service.</param>
    /// <param name="options">Base URL, timeout and request-size limit.</param>
    public AiWorkflowProvider(HttpClient http, IOptions<AiOptions> options)
    {
        _http = http;
        _options = options;
    }

    /// <inheritdoc/>
    public string Name => "AiWorkflow";

    /// <inheritdoc/>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(_options.Value.AiWorkflow.BaseUrl);

    /// <inheritdoc/>
    public Task<AiDraft?> DraftHandoffSummaryAsync(HandoffSummaryRequest request, CancellationToken ct)
    {
        // TODO(WS-9): refuse a request over MaxRequestBytes before sending; POST it as JSON to
        // {BaseUrl}/summaries/handoff under TimeoutSeconds; on 200 map the body to an AiDraft, on
        // 422 (the service refused its own model's output) and on anything else throw
        // AiDraftUnavailableException carrying the service's reason.
        throw new NotImplementedException("WS9: AiWorkflowProvider.DraftHandoffSummaryAsync");
    }
}
