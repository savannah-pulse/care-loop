namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// A summary drafted by an AI provider. Always a draft until a person approves it.
/// </summary>
/// <remarks>
/// Structured rather than free text so that each statement can be checked against the fact it
/// cites before anything is stored (decision 10). A draft whose sections cite an identifier that
/// was not supplied is refused: it means the provider wrote something it was not given.
/// </remarks>
public sealed record AiDraft
{
    /// <summary>The request this draft answers.</summary>
    public required Guid RequestId { get; init; }

    /// <summary>The drafted sections, in the order they should be read.</summary>
    public required IReadOnlyList<AiDraftSection> Sections { get; init; }

    /// <summary>Facts the provider found missing, stale or contradictory, in its own words, for a person to resolve.</summary>
    /// <remarks>A provider that cannot support a statement must list it here instead of writing the statement.</remarks>
    public required IReadOnlyList<string> MissingOrConflictingFacts { get; init; }

    /// <summary>Name of the provider that produced the draft.</summary>
    public required string ProviderName { get; init; }

    /// <summary>The model deployment used, as the provider reports it.</summary>
    public required string ModelDeployment { get; init; }

    /// <summary>Version of the prompt and template the provider used.</summary>
    public required string PromptVersion { get; init; }

    /// <summary>BCP 47 language tag the draft is written in.</summary>
    public required string Language { get; init; }
}
