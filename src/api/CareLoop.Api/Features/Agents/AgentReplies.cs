using CareLoop.Contracts.Ai;
using CareLoop.Contracts.Common;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Agents;

/// <summary>
/// The answers the three AI endpoints share: switched off, written, or could not be written.
/// </summary>
/// <remarks>
/// Three answers that can never be mistaken for one another, so a screen can always tell "AI is off", "AI wrote this"
/// and "AI could not write this" apart. There is no fourth answer with stand-in words.
/// </remarks>
internal static class AgentReplies
{
    /// <summary>The name the 501 answer gives in place of a workstream: these endpoints are switched on by configuration.</summary>
    public const string Workstream = "AI agents";

    /// <summary>Title of the 503 problem, in the words the dashboard uses.</summary>
    public const string UnavailableTitle = "AI could not write this";

    /// <summary>
    /// The 501 answer while <c>Ai:Agents:Enabled</c> is <see langword="false"/>.
    /// </summary>
    /// <param name="feature">The endpoint's feature name, for example <c>RoleBriefing</c>.</param>
    /// <returns>The existing not-implemented body with status 501.</returns>
    public static IResult Disabled(string feature) =>
        TypedResults.Json(
            new NotImplementedResponse
            {
                Workstream = Workstream,
                Feature = feature,
                Message = $"{feature} is switched off here: Ai:Agents:Enabled is false.",
            },
            statusCode: StatusCodes.Status501NotImplemented);

    /// <summary>
    /// Asks an agent and turns the outcome into the endpoint's answer.
    /// </summary>
    /// <param name="agents">The agent client.</param>
    /// <param name="agentName">The agent to ask.</param>
    /// <param name="prompt">The prompt, already limited to what the endpoint may send.</param>
    /// <param name="clock">Stamps when the text arrived.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>200 with <see cref="AiTextResponse"/>, or a calm 503 problem when the agent could not write the text.</returns>
    public static async Task<IResult> AskAsync(IAgentClient agents, string agentName, string prompt, IClock clock, CancellationToken ct)
    {
        string text;
        try
        {
            text = await agents.AskAsync(agentName, prompt, ct);
        }
        catch (AgentUnavailableException ex)
        {
            return Unavailable(agentName, ex.Message);
        }

        return string.IsNullOrWhiteSpace(text)
            ? Unavailable(agentName, "The agent wrote no text.")
            : TypedResults.Ok(new AiTextResponse { Agent = agentName, Text = text.Trim(), GeneratedAt = clock.UtcNow });
    }

    /// <summary>
    /// The 503 answer when an agent could not write the text.
    /// </summary>
    /// <param name="agentName">The agent that was asked.</param>
    /// <param name="reason">Why, in words safe to show staff; never the prompt.</param>
    /// <returns>A problem with status 503, a calm title and the agent's name.</returns>
    public static IResult Unavailable(string agentName, string reason) =>
        Results.Problem(
            title: UnavailableTitle,
            detail: $"{reason} Nothing else depends on it, so everything else still works. Try again later.",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            extensions: new Dictionary<string, object?> { ["agent"] = agentName });
}
