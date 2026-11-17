namespace CareLoop.Api.Features.Agents;

/// <summary>
/// Thrown when an agent could not write the text it was asked for.
/// </summary>
/// <remarks>
/// One exception for every way a call can fail (no token, 401, 403, 429, a server error, a timeout, an incomplete or
/// empty answer), so each endpoint has one failure to handle and always answers it the same way: a calm 503 and no text.
/// A success-shaped fallback would let staff read words nobody wrote as if AI had written them.
/// </remarks>
public sealed class AgentUnavailableException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="agentName">The agent that was asked.</param>
    /// <param name="reason">Why there is no text, in words safe to log and to show a member of staff. Never the prompt.</param>
    /// <param name="statusCode">The HTTP status the agent answered with, or <see langword="null"/> when it never answered.</param>
    /// <param name="innerException">The transport, token or parsing failure behind it, if any.</param>
    public AgentUnavailableException(string agentName, string reason, int? statusCode = null, Exception? innerException = null)
        : base(reason, innerException)
    {
        AgentName = agentName;
        StatusCode = statusCode;
    }

    /// <summary>The agent that was asked, for example <c>role-briefing</c>.</summary>
    public string AgentName { get; }

    /// <summary>The HTTP status the agent answered with, or <see langword="null"/> when it never answered (timeout, no token, no connection).</summary>
    public int? StatusCode { get; }
}
