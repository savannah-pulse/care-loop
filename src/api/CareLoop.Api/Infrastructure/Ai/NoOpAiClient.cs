using CareLoop.Api.Features.Ai;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>The AI client while <c>Ai:Provider</c> is <c>NoOp</c>: AI is off and nothing is ever sent.</summary>
public sealed class NoOpAiClient : IAiClient
{
    /// <inheritdoc/>
    public string Describe(string use) => "NoOp";

    /// <inheritdoc/>
    public bool IsEnabled => false;

    /// <inheritdoc/>
    public Task<string> CompleteAsync(AiRequest request, CancellationToken ct) => throw new AiUnavailableException("AI is switched off.");
}
