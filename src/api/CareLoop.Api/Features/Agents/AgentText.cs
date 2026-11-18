namespace CareLoop.Api.Features.Agents;

/// <summary>
/// What an agent wrote, and which agent version wrote it.
/// </summary>
/// <param name="Text">The text, trimmed and never empty.</param>
/// <param name="Agent">The agent version that answered: the one requested, which the client has checked against what
/// the provider reported, when it reported one.</param>
public sealed record AgentText(string Text, AgentReference Agent);
