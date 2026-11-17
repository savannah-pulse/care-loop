namespace CareLoop.Api.Features.Agents;

/// <summary>
/// Asks one Microsoft Foundry agent to write text from a prompt.
/// </summary>
/// <remarks>
/// <para>
/// The seam between the three AI endpoints and Foundry: <c>FoundryAgentClient</c> calls the project's agents, and tests
/// put a fake in its place to see exactly what would be sent. The endpoints build every prompt themselves, from the
/// fields their privacy rules allow, so nothing behind this interface ever sees a patient's name, contact details or
/// identifiers.
/// </para>
/// <para>
/// An agent gets a prompt and returns text. It is given no tool and no way to set urgency, change a case or send a
/// message, and whatever it writes is shown to staff as a draft. A call fails closed: an unreachable agent, a refusal,
/// a timeout or an empty answer raises <see cref="AgentUnavailableException"/>, never a stand-in text.
/// </para>
/// </remarks>
public interface IAgentClient
{
    /// <summary>
    /// Sends a prompt to an agent and returns the text it wrote.
    /// </summary>
    /// <param name="agentName">The agent's name in the Foundry project, for example <c>role-briefing</c>.</param>
    /// <param name="prompt">The plain-text prompt, built from the fields the calling endpoint allows.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The agent's text, trimmed, never empty.</returns>
    /// <exception cref="AgentUnavailableException">The agent could not be reached, refused, timed out or wrote nothing. No fallback text is ever returned.</exception>
    Task<string> AskAsync(string agentName, string prompt, CancellationToken ct);
}
