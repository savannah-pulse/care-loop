namespace CareLoop.Api.Features.Agents;

/// <summary>
/// One request to an agent: what it is for, which agent version answers, which prompt wording asks, and the prompt.
/// </summary>
/// <remarks>
/// Built by the three AI endpoints, which alone decide what a prompt may contain. The correlation identifier is written
/// to the read audit before the call, sent with the call and logged with its outcome, so one call can be followed from
/// the audit to the logs of CareLoop and of the AI provider.
/// </remarks>
public sealed record AgentRequest
{
    /// <summary>What the text is for: <c>role_briefing</c>, <c>impact_summary</c> or <c>patient_message_preview</c>.</summary>
    public required string Purpose { get; init; }

    /// <summary>The agent and the version pinned in <c>Ai:Agents</c>.</summary>
    public required AgentReference Agent { get; init; }

    /// <summary>The version of the prompt's wording, for example <c>role-briefing-v2</c>; it is also the prompt's first line.</summary>
    public required string PromptVersion { get; init; }

    /// <summary>The plain-text prompt, built only from the fields the calling endpoint allows.</summary>
    public required string Prompt { get; init; }

    /// <summary>Identifies this one call in the read audit, the logs and the request to the AI provider.</summary>
    public required Guid CorrelationId { get; init; }
}
