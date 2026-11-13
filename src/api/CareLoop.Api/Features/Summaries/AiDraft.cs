namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// Text drafted by an AI provider. Always a draft until a person approves it.
/// </summary>
public sealed record AiDraft
{
    /// <summary>The drafted text.</summary>
    public required string Text { get; init; }

    /// <summary>Name of the provider that produced it.</summary>
    public required string ProviderName { get; init; }

    /// <summary>The model or deployment used, when the provider reports it.</summary>
    public string? Model { get; init; }
}
