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
    /// Most times an agent is asked again after an answer CareLoop could not use: one that came back empty, in the wrong
    /// format or failing CareLoop's checks. Never after a refusal.
    /// </summary>
    public const int MaxRetries = 2;

    /// <summary>
    /// The time every attempt at one answer may take together, from <c>Ai:TimeoutSeconds</c>.
    /// </summary>
    /// <param name="ai">The shared AI settings.</param>
    /// <returns><c>Ai:TimeoutSeconds</c>, from 1 to <see cref="AiOptions.MaximumTimeoutSeconds"/> seconds.</returns>
    public static TimeSpan Budget(AiOptions ai) => TimeSpan.FromSeconds(Math.Clamp(ai.TimeoutSeconds, 1, AiOptions.MaximumTimeoutSeconds));

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
    /// Asks an agent, reads its structured answer and turns the outcome into the endpoint's answer, asking again, at
    /// most <see cref="MaxRetries"/> more times, when an answer could not be used.
    /// </summary>
    /// <param name="agents">The agent client.</param>
    /// <param name="request">The call, its prompt already limited to what the endpoint may send.</param>
    /// <param name="read">Turns the agent's JSON answer into the words staff read, checking it against what was sent;
    /// throws <see cref="AgentDeclinedException"/> when the agent declined or broke its output contract.</param>
    /// <param name="audit">Records one attempt in the read audit, with its own correlation identifier, before it is sent.</param>
    /// <param name="budget">The time every attempt together may take: <c>Ai:TimeoutSeconds</c>.</param>
    /// <param name="clock">Stamps when the text arrived.</param>
    /// <param name="logger">Records each unusable answer's reason code with its attempt's correlation identifier.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>200 with <see cref="AiTextResponse"/>, or a calm 503 problem when the agent could not write the text.</returns>
    /// <remarks>
    /// An answer is asked for again only when it came back empty, in the wrong format, or failed CareLoop's checks
    /// (<see cref="AgentDeclinedException.Refused"/> is <see langword="false"/>): the same question can get a usable answer
    /// from the same agent version. A refusal (<c>cannot_comply</c>) is the agent's judgement of the input and is never
    /// asked again, and neither is a failure to reach the agent (no token, 4xx, 5xx, 429), which another call at once
    /// would only repeat. Every attempt is audited before it is sent, with a fresh correlation identifier, and all of them
    /// together stay within <paramref name="budget"/>.
    /// </remarks>
    public static async Task<IResult> AskAsync(IAgentClient agents, AgentRequest request, Func<string, string> read,
        Func<AgentRequest, Task> audit, TimeSpan budget, IClock clock, ILogger logger, CancellationToken ct)
    {
        using var overall = CancellationTokenSource.CreateLinkedTokenSource(ct);
        overall.CancelAfter(budget);
        var attempt = request;
        for (var number = 1; ; number++)
        {
            await audit(attempt);
            AgentText answer;
            try
            {
                answer = await agents.AskAsync(attempt, overall.Token);
            }
            catch (AgentUnavailableException ex)
            {
                return Unavailable(request.Agent.Name, ex.Message);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Agent {Agent} did not answer request {CorrelationId} (attempt {Attempt} of {Attempts}) within the {Seconds} s allowed",
                    request.Agent, attempt.CorrelationId, number, MaxRetries + 1, (int)budget.TotalSeconds);
                var seconds = (int)budget.TotalSeconds;
                return Unavailable(request.Agent.Name, $"The agent did not answer within {(seconds == 1 ? "1 second" : $"{seconds} seconds")}.");
            }

            string reason;
            bool refused;
            try
            {
                var text = string.IsNullOrWhiteSpace(answer.Text) ? throw new AgentDeclinedException("EMPTY") : read(answer.Text);
                return string.IsNullOrWhiteSpace(text)
                    ? throw new AgentDeclinedException("EMPTY")
                    : TypedResults.Ok(new AiTextResponse
                    {
                        Agent = answer.Agent.Name, AgentVersion = answer.Agent.Version, PromptVersion = request.PromptVersion,
                        Text = text.Trim(), GeneratedAt = clock.UtcNow,
                    });
            }
            catch (AgentDeclinedException ex)
            {
                (reason, refused) = (ex.ReasonCode, ex.Refused);
            }

            logger.LogWarning("Agent {Agent} gave no usable draft for request {CorrelationId} (attempt {Attempt} of {Attempts}): {ReasonCode}",
                answer.Agent, attempt.CorrelationId, number, MaxRetries + 1, reason);
            if (refused || number > MaxRetries || overall.IsCancellationRequested)
            {
                return Unavailable(request.Agent.Name, "The agent did not write a draft this time.");
            }

            attempt = attempt with { CorrelationId = Guid.NewGuid() };
        }
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
