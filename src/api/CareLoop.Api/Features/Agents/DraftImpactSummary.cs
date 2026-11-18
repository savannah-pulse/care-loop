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
/// number, name or identifier is in it. CareLoop works out every number; the agent only describes what they show, in
/// neutral words and without saying why, and is asked not to repeat them, because the screen shows them beside its text.
/// </para>
/// <para>
/// Small numbers are hidden before anything is sent (ai-spec §4.3): a count from 1 to 4 is
/// written "fewer than 5", and a rate, median or average resting on fewer than
/// <see cref="SmallCountThreshold"/> cases, or with fewer than that on either side, is not given. The threshold is a
/// placeholder: the real one is an open governance decision. The prompt also names the period, the cohort, the version
/// of the measure definitions (<see cref="GetMetrics.DefinitionVersion"/>) and that the agents may be sent synthetic
/// data only, until the checks under "Before real data" are complete.
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
    public const string PromptVersion = "impact-summary-v2";

    /// <summary>The smallest count sent as it is; smaller ones are hidden. A placeholder until the governance decision on small-cell suppression.</summary>
    public const int SmallCountThreshold = 5;

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

    /// <summary>Asks the impact-summary agent to say in plain, neutral words what the aggregate numbers show.</summary>
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

        var scope = ReadAccess.FacilityScope(db.Cases.AsNoTracking(), actor, facilityId);
        var metrics = await GetMetrics.MeasureAsync(scope, db, pathways, clock.UtcNow, loggerFactory.CreateLogger(typeof(DraftImpactSummary)), ct);
        var opened = await scope.Select(c => c.CreatedAt).ToListAsync(ct);
        var request = new AgentRequest
        {
            Purpose = Purpose, Agent = options.Value.ImpactSummaryAgent(), PromptVersion = PromptVersion,
            Prompt = BuildPrompt(metrics, scopeFacility is null ? "every facility" : format.Facility(scopeFacility), format,
                opened.Count == 0 ? null : opened.Min()),
            CorrelationId = Guid.NewGuid(),
        };
        await AgentReplies.AuditAsync(db, actor, clock, AuditView, null, outside, reason, queryText, request, ct);
        return await AgentReplies.AskAsync(agents, request, clock, ct);
    }

    /// <summary>
    /// Writes the impact-summary prompt from aggregate measures, with small numbers hidden.
    /// </summary>
    /// <param name="metrics">The measures, as <c>GET /metrics</c> returns them.</param>
    /// <param name="scope">The facility's name, or <c>every facility</c>.</param>
    /// <param name="format">Gives local dates and times.</param>
    /// <param name="firstOpened">When the earliest case in the cohort was opened, or <see langword="null"/> when there is none.</param>
    /// <returns>The plain-text prompt. It holds totals only, never a case, and no count under <see cref="SmallCountThreshold"/>.</returns>
    internal static string BuildPrompt(MetricsResponse metrics, string scope, MessageFormat format, DateTimeOffset? firstOpened = null)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine($"CareLoop impact summary, prompt version {PromptVersion}.");
        prompt.AppendLine("Write a few short sentences on what these numbers say about follow-up: which steps have higher or lower completion, where open cases are waiting, and which recorded gaps are worth checking.");
        prompt.AppendLine("Describe only what the figures show, in neutral words such as \"acknowledgement not recorded by the due time\". Do not say why a figure is high or low, do not credit or blame any person, team or patient, do not give clinical advice and do not claim real-world results.");
        prompt.AppendLine("Use only the figures below and do not repeat them: the numbers are shown beside your words. Use plain words and British spelling.");
        prompt.AppendLine();
        prompt.AppendLine($"Cohort: every case at {scope}, open and closed, on every programme.");
        prompt.AppendLine(firstOpened is { } from
            ? $"Period: cases opened from {format.Date(from)} up to {AgentWords.Moment(metrics.GeneratedAt, format)}, local time."
            : $"Period: no case opened up to {AgentWords.Moment(metrics.GeneratedAt, format)}, local time.");
        prompt.AppendLine($"Measure definitions: version {GetMetrics.DefinitionVersion} of CareLoop's coordination measures.");
        prompt.AppendLine("Data: CareLoop may send AI agents synthetic data only until the checks under \"Before real data\" in its AI design are complete, so these figures come from synthetic cases, not real patients.");
        prompt.AppendLine($"Small numbers: any count from 1 to {SmallCountThreshold - 1} is written \"fewer than {SmallCountThreshold}\", and a rate, median or average resting on fewer than {SmallCountThreshold} cases, or with fewer than {SmallCountThreshold} on either side, is not given. The threshold is a placeholder: the real one is still an open governance decision. Do not guess or work out a hidden number.");
        prompt.AppendLine();
        prompt.AppendLine($"Cases: {Count(metrics.TotalCases)} in total, {Count(metrics.OpenCases)} open and {Count(metrics.ClosedCases)} closed.");
        prompt.AppendLine($"Open cases past their deadline: {Count(metrics.OverdueCases)}");
        prompt.AppendLine($"Open cases escalated: {Count(metrics.EscalatedCases)}");
        prompt.AppendLine($"Open cases with no named owner: {Count(metrics.UnassignedCases)}");
        prompt.AppendLine($"Why open cases are waiting: {Counts(metrics.CasesByException, ExceptionName, "nothing")}.");
        prompt.AppendLine($"Cases at each step now: {Counts(metrics.CasesByDisplayLabel, StepName, "none")}.");
        prompt.AppendLine();
        prompt.AppendLine("Steps completed, each with what it is out of:");
        foreach (var (key, measure) in metrics.Measures)
        {
            var (name, of) = MeasureWords.TryGetValue(key, out var words) ? words : (MessageFormat.Role(key), "eligible cases");
            prompt.AppendLine($"- {name}: {Rate(measure.Numerator, measure.Denominator, of)}.");
        }

        prompt.AppendLine();
        prompt.AppendLine("Median clock time from the screening result:");
        foreach (var (key, step) in metrics.TimeToStep)
        {
            var name = StepWords.TryGetValue(key, out var words) ? words : MessageFormat.Role(key);
            prompt.AppendLine(step.Median is null
                ? $"- to {name}: not reached yet."
                : step.Denominator < SmallCountThreshold
                    ? $"- to {name}: not given, fewer than {SmallCountThreshold} cases."
                    : $"- to {name}: {Duration(step.Median.Value)}, over {AgentWords.Count(step.Denominator, "case")}.");
        }

        prompt.AppendLine();
        prompt.AppendLine($"Closures by reason: {Counts(metrics.ClosuresByReason, ClosureName, "none yet")}.");
        prompt.AppendLine($"Confidence ratings after a call: {Confidence(metrics)}.");
        return prompt.ToString();
    }

    /// <summary>Gives a count, hiding a small one.</summary>
    /// <param name="count">The count.</param>
    /// <returns>The count; <c>fewer than 5</c> for 1 to 4.</returns>
    internal static string Count(int count) =>
        count is > 0 and < SmallCountThreshold ? $"fewer than {SmallCountThreshold}" : count.ToString(CultureInfo.InvariantCulture);

    /// <summary>Gives a rate with what it is out of, or says why it is not given.</summary>
    /// <param name="numerator">How many completed the step.</param>
    /// <param name="denominator">How many could have.</param>
    /// <param name="of">What the denominator counts, for example <c>patients told</c>.</param>
    /// <returns>For example <c>12 of 20 patients told (60%)</c>, or a reason it is not measured or not given.</returns>
    internal static string Rate(int numerator, int denominator, string of)
    {
        if (denominator == 0)
        {
            return $"not measured yet, no {of}";
        }

        if (denominator < SmallCountThreshold)
        {
            return $"not given, fewer than {SmallCountThreshold} {of}";
        }

        return IsSmall(numerator) || IsSmall(denominator - numerator)
            ? $"not given, fewer than {SmallCountThreshold} on one side, out of {denominator} {of}"
            : $"{numerator} of {denominator} {of} ({Percent((double)numerator / denominator)})";
    }

    private static string Confidence(MetricsResponse metrics)
    {
        var responses = metrics.ConfidenceResponses;
        var told = metrics.ConfidenceDenominator;
        if (responses == 0)
        {
            return $"none yet, out of {Count(told)} patients told";
        }

        return responses < SmallCountThreshold || IsSmall(told - responses)
            ? $"not given, fewer than {SmallCountThreshold} on one side, out of {Count(told)} patients told"
            : $"{responses} of {told} patients told gave one; average {metrics.AverageConfidence?.ToString("0.0", CultureInfo.InvariantCulture)} out of 5";
    }

    private static bool IsSmall(int count) => count is > 0 and < SmallCountThreshold;

    private static string Counts(IReadOnlyDictionary<string, int> counts, Func<string, string> name, string none)
    {
        var parts = counts.Where(c => c.Value > 0).Select(c => $"{name(c.Key)} {Count(c.Value)}").ToList();
        return parts.Count == 0 ? none : string.Join(", ", parts);
    }

    private static string ExceptionName(string key) =>
        Enum.TryParse<CaseExceptions>(key, out var exception) ? AgentWords.Exception(exception) : MessageFormat.Role(key);

    private static string StepName(string key) =>
        Enum.TryParse<CaseDisplayLabel>(key, out var label) ? AgentWords.StepName(label).ToLowerInvariant() : MessageFormat.Role(key);

    private static string ClosureName(string key) => ClosureWords.TryGetValue(key, out var words) ? words : MessageFormat.Role(key);

    private static string Percent(double rate) =>
        $"{Math.Round(rate * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%";

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
