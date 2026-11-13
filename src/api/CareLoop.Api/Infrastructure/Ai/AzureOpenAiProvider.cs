using CareLoop.Api.Features.Summaries;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Drafts handoff summaries with an Azure OpenAI chat deployment.
/// </summary>
/// <remarks>
/// Will send a fixed system prompt plus the coordination facts in the request (references,
/// statuses, timestamps; never patient names or clinical narrative) and return the model's text
/// as a draft. The prompt must instruct the model to summarise only what it is given and to state
/// nothing about diagnosis or urgency beyond repeating the reviewed urgency code. The result is
/// always a draft for a person to approve.
/// </remarks>
public sealed class AzureOpenAiProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly IOptions<AiOptions> _options;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="http">HTTP client used to call the deployment.</param>
    /// <param name="options">Endpoint, deployment and key, from the environment.</param>
    public AzureOpenAiProvider(HttpClient http, IOptions<AiOptions> options)
    {
        _http = http;
        _options = options;
    }

    /// <inheritdoc/>
    public string Name => "AzureOpenAi";

    /// <inheritdoc/>
    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(_options.Value.AzureOpenAi.Endpoint)
        && !string.IsNullOrWhiteSpace(_options.Value.AzureOpenAi.Deployment)
        && !string.IsNullOrWhiteSpace(_options.Value.AzureOpenAi.ApiKey);

    /// <inheritdoc/>
    /// <exception cref="HttpRequestException">The deployment could not be reached or returned an error.</exception>
    public Task<AiDraft?> DraftHandoffSummaryAsync(HandoffSummaryRequest request, CancellationToken ct)
    {
        // TODO(WS-9): build the prompt from the request, call the chat completions endpoint, return the text as a draft.
        throw new NotImplementedException("WS9: AzureOpenAiProvider.DraftHandoffSummaryAsync");
    }
}
