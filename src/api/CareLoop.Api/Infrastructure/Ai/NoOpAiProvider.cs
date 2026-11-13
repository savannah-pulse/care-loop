using CareLoop.Api.Features.Summaries;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// The default AI provider: AI is switched off and no draft is produced.
/// </summary>
/// <remarks>Lets every other feature run without any AI service or credential.</remarks>
public sealed class NoOpAiProvider : IAiProvider
{
    /// <inheritdoc/>
    public string Name => "NoOp";

    /// <inheritdoc/>
    public bool IsEnabled => false;

    /// <inheritdoc/>
    public Task<AiDraft?> DraftHandoffSummaryAsync(HandoffSummaryRequest request, CancellationToken ct) =>
        Task.FromResult<AiDraft?>(null);
}
