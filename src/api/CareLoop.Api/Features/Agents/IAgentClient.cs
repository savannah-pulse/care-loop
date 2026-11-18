namespace CareLoop.Api.Features.Agents;

/// <summary>
/// Asks one Microsoft Foundry agent, at a pinned version, to write text from a prompt.
/// </summary>
/// <remarks>
/// <para>
/// The seam between the three AI endpoints and whatever reaches the agents, chosen by the shared <c>Ai:Provider</c>
/// setting: <c>FoundryAgentClient</c> calls the Foundry project from the API (<c>FoundryDirect</c>),
/// <c>AiWorkflowAgentClient</c> hands the same request to the Python AI workflow service (<c>AiWorkflow</c>), and
/// <c>NoOpAgentClient</c> keeps AI off (<c>NoOp</c>). Which route the team uses can change here without touching the
/// endpoints. Tests put a fake in its place to see exactly what would be sent.
/// </para>
/// <para>
/// The endpoints build every prompt themselves, from the fields their privacy rules allow, so nothing behind this
/// interface ever sees a patient's name, contact details or identifiers. An agent gets a prompt and returns text. It is
/// given no tool and no way to set urgency, change a case or send a message, and whatever it writes is shown to staff
/// as a draft. A call fails closed: an unreachable agent, a refusal, a timeout, a request over the size limit, another
/// agent version or an empty answer raises <see cref="AgentUnavailableException"/>, never a stand-in text.
/// </para>
/// </remarks>
public interface IAgentClient
{
    /// <summary>The route this client takes: <c>FoundryDirect</c>, <c>AiWorkflow</c> or <c>NoOp</c>.</summary>
    string Route { get; }

    /// <summary><see langword="false"/> when AI is switched off (<c>Ai:Provider</c> is <c>NoOp</c>) and no agent can be asked.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Sends a prompt to the requested agent version and returns the text it wrote.
    /// </summary>
    /// <param name="request">The purpose, the pinned agent version, the prompt version, the prompt and the correlation identifier.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The agent's text, trimmed and never empty, with the agent version that wrote it.</returns>
    /// <exception cref="AgentUnavailableException">The agent could not be reached, refused, timed out, answered from another
    /// version or wrote nothing, or the request was over the size limit. No fallback text is ever returned.</exception>
    Task<AgentText> AskAsync(AgentRequest request, CancellationToken ct);
}
