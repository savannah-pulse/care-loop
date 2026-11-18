using CareLoop.Api.Features.Agents;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// The agent client while AI is switched off (<c>Ai:Provider</c> is <c>NoOp</c>, the default): no agent can be asked.
/// </summary>
/// <remarks>
/// The endpoints check <see cref="IsEnabled"/> and answer 409 before building any prompt, so this client is never
/// asked. If it were, it would fail closed rather than send anything or invent text.
/// </remarks>
public sealed class NoOpAgentClient : IAgentClient
{
    /// <inheritdoc/>
    public string Route => nameof(AiRoute.NoOp);

    /// <inheritdoc/>
    public bool IsEnabled => false;

    /// <inheritdoc/>
    public Task<AgentText> AskAsync(AgentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        throw new AgentUnavailableException(request.Agent.Name, "AI is switched off here.");
    }
}
