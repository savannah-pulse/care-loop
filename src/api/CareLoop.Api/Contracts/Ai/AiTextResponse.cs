namespace CareLoop.Contracts.Ai;

/// <summary>
/// Text a Microsoft Foundry agent wrote for a member of staff to read, returned by the three AI endpoints.
/// </summary>
/// <remarks>
/// AI output is a draft. CareLoop shows it labelled as written by AI and never acts on it: it is not stored, it sets no
/// urgency, changes no case and sends no message. When the agent cannot write it, the endpoint answers 503 with no text
/// at all, so this response only ever carries words the agent actually wrote. The agent version and the prompt version
/// say exactly which wording produced it; the read audit records both with the call.
/// </remarks>
public sealed record AiTextResponse
{
    /// <summary>The agent that wrote the text, as named in the Foundry project, for example <c>role-briefing</c>.</summary>
    public required string Agent { get; init; }

    /// <summary>The agent version that wrote it, as pinned in <c>Ai:Agents</c>, for example <c>2</c>.</summary>
    public required string AgentVersion { get; init; }

    /// <summary>The version of the prompt's wording that asked for it, for example <c>role-briefing-v2</c>.</summary>
    public required string PromptVersion { get; init; }

    /// <summary>The text exactly as the agent wrote it, trimmed. Plain text, never markup to render.</summary>
    public required string Text { get; init; }

    /// <summary>When CareLoop received the text.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }
}
