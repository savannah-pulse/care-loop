using System.Globalization;
using System.Text;
using CareLoop.Api.Features.Common;
using CareLoop.Api.Features.Metrics;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Ai;
using CareLoop.Contracts.Metrics;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Features.Agents;

/// <summary>
/// Slice for <c>POST /ai/impact-summary</c>: asks the impact-summary agent what the numbers on the Impact screen say.
/// </summary>
/// <remarks>
/// <para>
/// The prompt is built from the aggregate measures only, computed exactly as <c>GET /metrics</c> computes them for the
/// same reader and scope: counts, rates with what each is out of, median times and closures by reason. No case, case
/// number, name or identifier is in it. CareLoop works out every number; the agent only puts into words what they
/// show, and is asked not to repeat them, because the screen shows them beside its text.
/// </para>
/// <para>
/// Access follows <c>GET /metrics</c>: staff see their facility unless they give a reason for another, and the
/// programme coordinator sees every facility. The read is recorded, with the agent version, prompt version and
/// correlation identifier, and what the agent writes is a draft that is not stored and changes nothing.
/// </para>
/// </remarks>
public static class DraftImpactSummary
{
    /// <summary>The view key the read is recorded under; a refused read appends <c>-denied</c>.</summary>
    public const string AuditView = "impact-summary";

    /// <summary>What the text is for, as the request to the AI provider names it.</summary>
    public const string Purpose = "impact_summary";

    /// <summary>The prompt's version, written into the prompt and returned with the text so a change of wording can be traced.</summary>
    public const string PromptVersion = "impact-summary-v1";

    private static readonly IReadOnlyDictionary<string, (string Name, string Of)> MeasureWords = new Dictionary<string, (string, string)>
    {
        ["nextStepOnTime"] = ("Next care step done by the deadline", "cases whose deadline has passed"),
        ["understanding"] = ("Understood their next step", "patients who acknowledged"),
        ["acknowledgement"] = ("Patients who acknowledged the message", "patients told"),
        ["closure"] = ("Cases closed", "cases"),
        ["referralAcceptedAndAttended"] = ("Referral accepted and attended", "referrals"),
        ["appointmentsAttended"] = ("Appointments attended", "appointments due"),
        ["treatmentStartedOnTime"] = ("Treatment started by the deadline", "people who needed treatment"),
        ["signedEvidence"] = ("Closed with signed evidence", "closed cases"),
    };

    private static readonly IReadOnlyDictionary<string, string> StepWords = new Dictionary<string, string>
    {
        ["acknowledgement"] = "patient acknowledged",
        ["consultation"] = "consultation answered",
        ["appointment"] = "appointment attended",
        ["careStarted"] = "care started (treatment or surveillance)",
        ["closure"] = "case closed",
    };

    private static readonly IReadOnlyDictionary<string, string> ClosureWords = new Dictionary<string, string>
    {
        [nameof(ClosureReason.Treated)] = "treated",
        [nameof(ClosureReason.OnSurveillance)] = "on surveillance",
        [nameof(ClosureReason.NegativeOnConfirmation)] = "negative on confirmation",
        [nameof(ClosureReason.Declined)] = "declined by the patient",
        [nameof(ClosureReason.TransferredAndAccepted)] = "transferred and accepted elsewhere",
        [nameof(ClosureReason.Untraceable)] = "untraceable after documented attempts",
    };

    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapDraftImpactSummary(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/ai/impact-summary", Handle).WithName("DraftImpactSummary").WithTags("Ai")
            .Produces<AiTextResponse>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return routes;
    }

    /// <summary>Asks the impact-summary agent to say in plain words what the aggregate numbers show.</summary>
    /// <param name="facilityId">Optional facility, as for <c>GET /metrics</c>; the coordinator otherwise gets every facility.</param>
    /// <param name="reason">Reason for staff reading another facility's numbers, recorded with the read.</param>
    /// <param name="context">The caller-stated staff identity.</param>
    /// <param name="db">The read context.</param>
    /// <param name="pathways">The display-label and closure rules the measures use.</param>
    /// <param name="format">Gives local dates, times and facility names.</param>
    /// <param name="agents">Calls the agent on the route <c>Ai:Provider</c> chose.</param>
    /// <param name="options">Whether the agents are on, and the agent version.</param>
    /// <param name="clock">The measurement time and the read audit clock.</param>
    /// <param name="loggerFactory">Records cases left out of a measure, as <c>GET /metrics</c> does.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// 200 with the agent's text; 400, 401 or 403 as for <c>GET /metrics</c>; 409 while the agents are switched off
    /// (<c>Ai:Agents:Enabled</c> is <see langword="false"/> or <c>Ai:Provider</c> is <c>NoOp</c>); 503 when the agent
    /// could not write it.
    /// </returns>
    public static async Task<IResult> Handle([FromQuery] string? facilityId, [FromQuery] string? reason, HttpContext context,
        CareLoopDbContext db, IPathwayProvider pathways, MessageFormat format, IAgentClient agents, IOptions<AgentOptions> options,
        IClock clock, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        if (ReadAccess.Identity(context, out var actor) is { } error) return error;
        if (ReadAccess.ValidateReason(reason) is { } invalid) return invalid;
        if (facilityId?.Length > 100 || facilityId is not null && string.IsNullOrWhiteSpace(facilityId))
            return Results.Problem("Invalid facility filter.", statusCode: 400);
        if (!AgentReplies.IsOn(options.Value, agents)) return AgentReplies.SwitchedOff("ImpactSummary", options.Value);

        var scopeFacility = facilityId ?? (actor.Role == DemoRoles.Coordinator ? null : actor.FacilityId);
        var queryText = ReadAccess.Query(("facilityId", scopeFacility));
        var outside = actor.Role != DemoRoles.Coordinator && facilityId is not null && facilityId != actor.FacilityId;
        if (ReadAccess.CrossFacilityPermission(actor, facilityId, reason) is { } denied)
        {
            await ReadAccess.AuditAsync(db, actor, clock, AuditView + "-denied", null, outside, reason, queryText, ct);
            return denied;
        }

        var metrics = await GetMetrics.MeasureAsync(ReadAccess.FacilityScope(db.Cases.AsNoTracking(), actor, facilityId),
            db, pathways, clock.UtcNow, loggerFactory.CreateLogger(typeof(DraftImpactSummary)), ct);
        var request = new AgentRequest
        {
            Purpose = Purpose, Agent = options.Value.ImpactSummaryAgent(), PromptVersion = PromptVersion,
            Prompt = BuildPrompt(metrics, scopeFacility is null ? "every facility" : format.Facility(scopeFacility), format),
            CorrelationId = Guid.NewGuid(),
        };
        await AgentReplies.AuditAsync(db, actor, clock, AuditView, null, outside, reason, queryText, request, ct);
        return await AgentReplies.AskAsync(agents, request, clock, ct);
    }

    /// <summary>
    /// Writes the impact-summary prompt from aggregate measures.
    /// </summary>
    /// <param name="metrics">The measures, as <c>GET /metrics</c> returns them.</param>
    /// <param name="scope">The facility's name, or <c>every facility</c>.</param>
    /// <param name="format">Gives local dates and times.</param>
    /// <returns>The plain-text prompt. It holds totals only, never a case.</returns>
    internal static string BuildPrompt(MetricsResponse metrics, string scope, MessageFormat format)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine($"CareLoop impact summary, prompt version {PromptVersion}.");
        prompt.AppendLine("Write a few short sentences on what these numbers say about follow-up: what is going well, where cases are waiting, and anything worth checking.");
        prompt.AppendLine("Use only the figures below and do not repeat them: the numbers are shown beside your words. Use plain words and British spelling. Describe what the numbers show; do not give clinical advice or claim real-world results.");
        prompt.AppendLine();
        prompt.AppendLine($"Scope: {scope}.");
        prompt.AppendLine($"Measured: {AgentWords.Moment(metrics.GeneratedAt, format)}, local time.");
        prompt.AppendLine();
        prompt.AppendLine($"Cases: {metrics.TotalCases} in total, {metrics.OpenCases} open and {metrics.ClosedCases} closed.");
        prompt.AppendLine($"Open cases past their deadline: {metrics.OverdueCases}");
        prompt.AppendLine($"Open cases escalated: {metrics.EscalatedCases}");
        prompt.AppendLine($"Open cases with no named owner: {metrics.UnassignedCases}");
        prompt.AppendLine($"Why open cases are waiting: {Counts(metrics.CasesByException, ExceptionName, "nothing")}.");
        prompt.AppendLine($"Cases at each step now: {Counts(metrics.CasesByDisplayLabel, StepName, "none")}.");
        prompt.AppendLine();
        prompt.AppendLine("Steps completed, each with what it is out of:");
        foreach (var (key, measure) in metrics.Measures)
        {
            var (name, of) = MeasureWords.TryGetValue(key, out var words) ? words : (MessageFormat.Role(key), "eligible cases");
            prompt.AppendLine(measure.Denominator == 0
                ? $"- {name}: not measured yet, no {of}."
                : $"- {name}: {measure.Numerator} of {measure.Denominator} {of} ({Percent(measure.Rate)}).");
        }

        prompt.AppendLine();
        prompt.AppendLine("Median clock time from the screening result:");
        foreach (var (key, step) in metrics.TimeToStep)
        {
            var name = StepWords.TryGetValue(key, out var words) ? words : MessageFormat.Role(key);
            prompt.AppendLine(step.Median is { } median
                ? $"- to {name}: {Duration(median)}, over {AgentWords.Count(step.Denominator, "case")}."
                : $"- to {name}: not reached yet.");
        }

        prompt.AppendLine();
        prompt.AppendLine($"Closures by reason: {Counts(metrics.ClosuresByReason, ClosureName, "none yet")}.");
        prompt.AppendLine(metrics.ConfidenceResponses == 0
            ? $"Confidence ratings after a call: none yet, out of {AgentWords.Count(metrics.ConfidenceDenominator, "patient")} told."
            : $"Confidence ratings after a call: {metrics.ConfidenceResponses} of {metrics.ConfidenceDenominator} patients told gave one; average {metrics.AverageConfidence?.ToString("0.0", CultureInfo.InvariantCulture)} out of 5.");
        return prompt.ToString();
    }

    private static string Counts(IReadOnlyDictionary<string, int> counts, Func<string, string> name, string none)
    {
        var parts = counts.Where(c => c.Value > 0).Select(c => $"{name(c.Key)} {c.Value}").ToList();
        return parts.Count == 0 ? none : string.Join(", ", parts);
    }

    private static string ExceptionName(string key) =>
        Enum.TryParse<CaseExceptions>(key, out var exception) ? AgentWords.Exception(exception) : MessageFormat.Role(key);

    private static string StepName(string key) =>
        Enum.TryParse<CaseDisplayLabel>(key, out var label) ? AgentWords.StepName(label).ToLowerInvariant() : MessageFormat.Role(key);

    private static string ClosureName(string key) => ClosureWords.TryGetValue(key, out var words) ? words : MessageFormat.Role(key);

    private static string Percent(double? rate) =>
        rate is { } value ? $"{Math.Round(value * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%" : "not measured";

    /// <summary>Gives a median duration in the two largest units that matter.</summary>
    /// <param name="span">The duration.</param>
    /// <returns>For example <c>1 day 4 hours</c>, <c>5 hours 20 minutes</c>, <c>35 minutes</c> or <c>under a minute</c>.</returns>
    internal static string Duration(TimeSpan span)
    {
        if (span.TotalMinutes < 1) return "under a minute";
        if (span.TotalHours < 1) return AgentWords.Count((int)span.TotalMinutes, "minute");
        if (span.TotalDays < 1)
        {
            return span.Minutes == 0
                ? AgentWords.Count(span.Hours, "hour")
                : $"{AgentWords.Count(span.Hours, "hour")} {AgentWords.Count(span.Minutes, "minute")}";
        }

        return span.Hours == 0
            ? AgentWords.Count(span.Days, "day")
            : $"{AgentWords.Count(span.Days, "day")} {AgentWords.Count(span.Hours, "hour")}";
    }
}
