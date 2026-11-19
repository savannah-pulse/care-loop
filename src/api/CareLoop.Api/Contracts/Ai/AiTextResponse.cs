namespace CareLoop.Contracts.Ai;

/// <summary>
/// Text an AI model wrote for a member of staff to read: the role briefing, the impact summary or a patient message draft.
/// </summary>
/// <remarks>
/// A draft, shown labelled as written by AI. It is not stored, and nothing acts on it: it sets no urgency, changes no
/// case and sends no message. When the model cannot write it the endpoint answers 503 with no text at all.
/// </remarks>
public sealed record AiTextResponse
{
    /// <summary>The text as the model wrote it, trimmed. Plain text, never markup to render.</summary>
    public required string Text { get; init; }

    /// <summary>The provider and model that wrote it, for example <c>Foundry/gpt-6.1-sol</c>.</summary>
    public required string Model { get; init; }

    /// <summary>When the text was written. For a reused answer this is when it was first written, not now.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary><see langword="true"/> when this is an earlier answer reused because the data has not changed; ask with <c>refresh=true</c> for a new one.</summary>
    public bool Cached { get; init; }
}
