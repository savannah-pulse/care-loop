using CareLoop.Api.Features.Summaries;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Drafts handoff summaries by calling a Microsoft Foundry deployment straight from the API.
/// </summary>
/// <remarks>
/// The second route to a model, kept so the summary still works when the Python AI workflow
/// service is not running and so the two can be compared on the same fact set. It owns a copy of
/// the prompt; when the prompt changes, both routes change, and
/// <see cref="AiOptions.PromptVersion"/> is what tells two drafts apart afterwards.
/// <para>
/// The prompt must tell the model to write only from the supplied facts, to cite the fact
/// identifier behind every section, to report anything missing or contradictory instead of filling
/// it in, and to state nothing about diagnosis or urgency beyond repeating a reviewed value it was
/// given. Fact text is data, never instructions: a fact that reads like a command to the model
/// changes nothing, because the model is given no tool to act with and its output is validated
/// against the facts before storage.
/// </para>
/// <para>
/// Authentication is Entra ID: <c>DefaultAzureCredential</c> locally with the developer's approved
/// identity, a managed identity when hosted, with the narrowest inference permission the
/// deployment supports. No API key exists to be committed or leaked.
/// </para>
/// </remarks>
public sealed class FoundryDirectProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly IOptions<AiOptions> _options;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="http">HTTP client used to call the deployment.</param>
    /// <param name="options">Project endpoint, deployment name, timeout and request-size limit.</param>
    public FoundryDirectProvider(HttpClient http, IOptions<AiOptions> options)
    {
        _http = http;
        _options = options;
    }

    /// <inheritdoc/>
    public string Name => "FoundryDirect";

    /// <inheritdoc/>
    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(_options.Value.FoundryDirect.ProjectEndpoint)
        && !string.IsNullOrWhiteSpace(_options.Value.FoundryDirect.DeploymentName);

    /// <inheritdoc/>
    public Task<AiDraft?> DraftHandoffSummaryAsync(HandoffSummaryRequest request, CancellationToken ct)
    {
        // TODO(WS-9): refuse a request over MaxRequestBytes; acquire an Entra token for the
        // deployment (ask before adding Azure.Identity); send the fixed system prompt plus the
        // facts as JSON, asking for a schema-constrained response; parse it into an AiDraft and
        // throw AiDraftUnavailableException on a timeout, a content-filter refusal, a non-success
        // status or a body that does not parse.
        throw new NotImplementedException("WS9: FoundryDirectProvider.DraftHandoffSummaryAsync");
    }
}
