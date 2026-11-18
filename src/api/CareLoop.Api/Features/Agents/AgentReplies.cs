using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Api.Infrastructure.Persistence.Audit;
using CareLoop.Contracts.Ai;
using CareLoop.Domain.Events;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Agents;

/// <summary>
/// The answers the three AI endpoints share: switched off, written, or could not be written; and the read audit of a call.
/// </summary>
/// <remarks>
/// Three answers that can never be mistaken for one another, so a screen can always tell "AI is off" (409), "AI wrote
/// this" (200) and "AI could not write this" (503) apart. There is no fourth answer with stand-in words. 409 is the
/// handoff summary's answer while AI is off, so every AI endpoint says it the same way.
/// </remarks>
internal static class AgentReplies
{
    /// <summary>Title of the 409 problem while AI is switched off.</summary>
    public const string SwitchedOffTitle = "AI is switched off";

    /// <summary>Title of the 503 problem, in the words the dashboard uses.</summary>
    public const string UnavailableTitle = "AI could not write this";

    /// <summary>
    /// Tells whether the agents may be asked: <c>Ai:Agents:Enabled</c> is <see langword="true"/> and <c>Ai:Provider</c>
    /// names a route.
    /// </summary>
    /// <param name="options">The agents' settings.</param>
    /// <param name="agents">The client <c>Ai:Provider</c> chose.</param>
    /// <returns><see langword="true"/> when both are on.</returns>
    public static bool IsOn(AgentOptions options, IAgentClient agents) => options.Enabled && agents.IsEnabled;

    /// <summary>
    /// The 409 answer while the agents are switched off: <c>Ai:Agents:Enabled</c> is <see langword="false"/> or
    /// <c>Ai:Provider</c> is <c>NoOp</c>.
    /// </summary>
    /// <param name="feature">The endpoint's feature name, for example <c>RoleBriefing</c>.</param>
    /// <param name="options">The agents' settings, to say which switch is off.</param>
    /// <returns>A problem with status 409; nothing was read or sent.</returns>
    public static IResult SwitchedOff(string feature, AgentOptions options) =>
        Results.Problem(
            title: SwitchedOffTitle,
            detail: options.Enabled
                ? $"{feature} is switched off here: Ai:Provider is NoOp. Nothing was sent."
                : $"{feature} is switched off here: Ai:Agents:Enabled is false. Nothing was sent.",
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?> { ["feature"] = feature });

    /// <summary>
    /// Records an AI call in the read audit, with the agent version, the prompt version and the correlation identifier,
    /// before the call is made.
    /// </summary>
    /// <param name="db">The read context.</param>
    /// <param name="actor">Who asked.</param>
    /// <param name="clock">Supplies the read time.</param>
    /// <param name="view">The endpoint's view key, for example <c>role-briefing</c>.</param>
    /// <param name="caseId">The case, or <see langword="null"/> for a worklist or measures read.</param>
    /// <param name="outsideTeam">Whether the reader looked at another team's cases.</param>
    /// <param name="reason">The reason given for that, if any.</param>
    /// <param name="query">The request's filters.</param>
    /// <param name="request">The call about to be made.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>Completion of the append-only access audit; a failure fails the request before anything is sent.</returns>
    public static async Task AuditAsync(CareLoopDbContext db, Actor actor, IClock clock, string view, Guid? caseId,
        bool outsideTeam, string? reason, string? query, AgentRequest request, CancellationToken ct)
    {
        db.CaseReads.Add(new CaseReadRecord
        {
            Id = Guid.NewGuid(), CaseId = caseId, ActorReference = actor.Reference, ActorRole = actor.Role,
            ActorFacilityId = actor.FacilityId, View = view, OutsideTeam = outsideTeam, Reason = reason, Query = query,
            ReadAt = clock.UtcNow, AgentName = request.Agent.Name, AgentVersion = request.Agent.Version,
            PromptVersion = request.PromptVersion, CorrelationId = request.CorrelationId,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Asks an agent and turns the outcome into the endpoint's answer.
    /// </summary>
    /// <param name="agents">The agent client.</param>
    /// <param name="request">The call, its prompt already limited to what the endpoint may send.</param>
    /// <param name="clock">Stamps when the text arrived.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>200 with <see cref="AiTextResponse"/>, or a calm 503 problem when the agent could not write the text.</returns>
    public static async Task<IResult> AskAsync(IAgentClient agents, AgentRequest request, IClock clock, CancellationToken ct)
    {
        AgentText answer;
        try
        {
            answer = await agents.AskAsync(request, ct);
        }
        catch (AgentUnavailableException ex)
        {
            return Unavailable(request.Agent.Name, ex.Message);
        }

        return string.IsNullOrWhiteSpace(answer.Text)
            ? Unavailable(request.Agent.Name, "The agent wrote no text.")
            : TypedResults.Ok(new AiTextResponse
            {
                Agent = answer.Agent.Name, AgentVersion = answer.Agent.Version, PromptVersion = request.PromptVersion,
                Text = answer.Text.Trim(), GeneratedAt = clock.UtcNow,
            });
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
