using System.Globalization;
using System.Text.Json;
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
/// The agent (AG3 in the Foundry project) reads the aggregate measures inside <c>&lt;input_json&gt;</c>, computed
/// exactly as <c>GET /metrics</c> computes them for the same reader and scope: counts and rates with what each is out
/// of, median times, why open cases wait and closures by reason, each with a metric ID. No case, case number, name or
/// identifier is in it. CareLoop works out every number; the agent picks which to describe, in two to four neutral,
/// non-causal sentences that each name the metric IDs they rest on, and adds a separate note on what the figures cannot
/// show. A sentence citing a metric ID that was not sent, or a refusal (<c>cannot_comply</c>), is the calm 503, with the
/// reason code in the log.
/// </para>
/// <para>
/// Small numbers are suppressed before anything is sent (ai-spec §4.3): a count from 1 to 4, and a rate, median or
/// average resting on fewer than <see cref="SmallCountThreshold"/> cases, or with fewer than that on either side, is sent
/// as <c>"suppressed": true</c> with no number. The threshold is a placeholder: the real one is an open governance
/// decision. The prompt also names the period, the cohort, the version of the measure definitions
/// (<see cref="GetMetrics.DefinitionVersion"/>) and that the data is synthetic, as it must be until the checks under
/// "Before real data" are complete.
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
    public const string PromptVersion = "impact-summary-v3";

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

    private static readonly IReadOnlyDictionary<string, string> DelayWords = new Dictionary<string, string>
    {
        [nameof(CaseExceptions.Overdue)] = "past the deadline",
        [nameof(CaseExceptions.Unreachable)] = "patient not reached",
        [nameof(CaseExceptions.Rejected)] = "referral declined",
        [nameof(CaseExceptions.Unacknowledged)] = "message not acknowledged",
        [nameof(CaseExceptions.Unassigned)] = "no named owner",
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
        var input = BuildInput(metrics, scopeFacility is null ? "every facility" : format.Facility(scopeFacility), format,
            opened.Count == 0 ? null : opened.Min());
        var request = new AgentRequest
        {
            Purpose = Purpose, Agent = options.Value.ImpactSummaryAgent(), PromptVersion = PromptVersion, Prompt = input.Prompt,
            CorrelationId = Guid.NewGuid(),
        };
        await AgentReplies.AuditAsync(db, actor, clock, AuditView, null, outside, reason, queryText, request, ct);
        return await AgentReplies.AskAsync(agents, request, answer => Read(answer, input), clock,
            loggerFactory.CreateLogger(typeof(DraftImpactSummary)), ct);
    }

    /// <summary>
    /// Writes the impact-summary prompt: the aggregate measures, with small numbers suppressed, inside
    /// <c>&lt;input_json&gt;</c>.
    /// </summary>
    /// <param name="metrics">The measures, as <c>GET /metrics</c> returns them.</param>
    /// <param name="scope">The facility's name, or <c>every facility</c>.</param>
    /// <param name="format">Gives local dates and times.</param>
    /// <param name="firstOpened">When the earliest case in the cohort was opened, or <see langword="null"/> when there is none.</param>
    /// <returns>The prompt and the metric IDs it holds. Totals only, never a case, and no count under <see cref="SmallCountThreshold"/>.</returns>
    internal static ImpactInput BuildInput(MetricsResponse metrics, string scope, MessageFormat format, DateTimeOffset? firstOpened = null)
    {
        var generated = AgentInput.Moment(format.InLocalTime(metrics.GeneratedAt));
        // Total = open + closed, so when either is small the other two would give it away: send neither total.
        var hideTotals = IsSmall(metrics.TotalCases) || IsSmall(metrics.OpenCases) || IsSmall(metrics.ClosedCases);
        var measures = new List<object>
        {
            Measure("overdue_open", "Open cases past their deadline", metrics.OverdueCases, metrics.OpenCases, "open cases"),
            Measure("escalated_open", "Open cases escalated", metrics.EscalatedCases, metrics.OpenCases, "open cases"),
            Measure("unassigned_open", "Open cases with no named owner", metrics.UnassignedCases, metrics.OpenCases, "open cases"),
        };
        foreach (var (key, measure) in metrics.Measures.Where(m => m.Value.Denominator > 0))
        {
            var (name, of) = MeasureWords.TryGetValue(key, out var words) ? words : (MessageFormat.Role(key), "eligible cases");
            measures.Add(Measure(key, name, measure.Numerator, measure.Denominator, of));
        }

        var stepTimes = metrics.TimeToStep.Where(s => s.Value.Median is not null).Select(s => (object)(s.Value.Denominator < SmallCountThreshold
            ? new { MetricId = $"time_to_{s.Key}", Label = $"Median clock time from the screening result to {StepWord(s.Key)}", Suppressed = true }
            : new
            {
                MetricId = $"time_to_{s.Key}", Label = $"Median clock time from the screening result to {StepWord(s.Key)}",
                Median = Duration(s.Value.Median!.Value), OverCases = s.Value.Denominator, ComparisonMedian = (string?)null, Suppressed = false,
            })).ToList();

        var delays = metrics.CasesByException.Where(e => e.Value > 0).Select(e => new
        {
            MetricId = $"waiting_{e.Key}", Code = e.Key, Label = DelayWords.TryGetValue(e.Key, out var delay) ? delay : ExceptionName(e.Key),
            Count = IsSmall(e.Value) ? (int?)null : e.Value, Suppressed = IsSmall(e.Value),
        }).ToList();
        // Closure reasons split the closed cases between them, so one small count could be worked out from the others:
        // when exactly one is small, the next smallest is suppressed with it.
        var reasons = metrics.ClosuresByReason.Where(c => c.Value > 0).OrderBy(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal).ToList();
        var alsoHidden = reasons.Count(r => IsSmall(r.Value)) == 1 ? reasons.FirstOrDefault(r => !IsSmall(r.Value)).Key : null;
        var supplementary = new List<object>();
        foreach (var (key, count) in reasons)
        {
            supplementary.Add(Measure($"closed_{key}", $"Closed cases: {ClosureName(key)}", count, metrics.ClosedCases, "closed cases",
                suppress: key == alsoHidden));
        }

        if (metrics.ConfidenceDenominator > 0)
        {
            supplementary.Add(Measure("confidence_responses", "Patients told who gave a confidence rating after a call",
                metrics.ConfidenceResponses, metrics.ConfidenceDenominator, "patients told"));
        }

        if (metrics.AverageConfidence is { } average && metrics.ConfidenceResponses >= SmallCountThreshold)
        {
            supplementary.Add(new
            {
                MetricId = "confidence_average", Label = "Average confidence rating after a call", Value = Math.Round(average, 1),
                Unit = "out of 5", OverCases = metrics.ConfidenceResponses, Suppressed = false,
            });
        }

        var input = new
        {
            SnapshotId = $"{GetMetrics.DefinitionVersion}@{generated}",
            Period = new
            {
                From = firstOpened is { } from ? AgentInput.Day(DateOnly.FromDateTime(format.InLocalTime(from).DateTime)) : null,
                To = generated,
                Covers = "cases opened from this day up to this moment, open and closed",
            },
            Filters = new { Programme = "every programme", Facility = scope },
            CohortSize = hideTotals ? (int?)null : metrics.TotalCases,
            CohortSizeSuppressed = hideTotals,
            OpenCases = hideTotals ? (int?)null : metrics.OpenCases,
            Synthetic = true,
            MeasureDefinitions = GetMetrics.DefinitionVersion,
            SmallCellThreshold = SmallCountThreshold,
            Measures = measures,
            StepTimes = stepTimes,
            Funnel = Array.Empty<object>(),
            DelayReasons = delays,
            DelayReasonsTotal = delays.Sum(d => d.Count ?? 0),
            Supplementary = supplementary,
            Targets = Array.Empty<object>(),
            MaxWords = 120,
        };
        var ids = new HashSet<string>(StringComparer.Ordinal) { "cohort_size", "delay_reasons_total" };
        using (var facts = JsonDocument.Parse(JsonSerializer.Serialize(new { measures, stepTimes, delays, supplementary })))
        {
            foreach (var group in facts.RootElement.EnumerateObject())
            {
                foreach (var fact in group.Value.EnumerateArray())
                {
                    ids.Add(fact.GetProperty("MetricId").GetString()!);
                }
            }
        }

        return new ImpactInput(AgentInput.Prompt("CareLoop impact summary", PromptVersion, input), ids);
    }

    /// <summary>
    /// Reads the agent's summary: two to four sentences, each naming the metric IDs it relies on, and a separate note on
    /// what the figures cannot show.
    /// </summary>
    /// <param name="answer">The agent's answer.</param>
    /// <param name="input">What was sent.</param>
    /// <returns>The sentences as one paragraph, then the limits note as a second.</returns>
    /// <exception cref="AgentDeclinedException">The agent declined, wrote no sentence or more than four, cited a metric ID
    /// that was not sent, or left out the limits note.</exception>
    internal static string Read(string answer, ImpactInput input)
    {
        var root = AgentReply.Read(answer);
        var sentences = AgentReply.Objects(root, "sentences");
        if (sentences.Count is < 1 or > 4)
        {
            throw new AgentDeclinedException("SENTENCE_COUNT");
        }

        var texts = new List<string>();
        foreach (var sentence in sentences)
        {
            if (AgentReply.Texts(sentence, "metric_ids").Any(id => !input.MetricIds.Contains(id)))
            {
                throw new AgentDeclinedException("UNKNOWN_METRIC_ID");
            }

            texts.Add(AgentReply.RequiredText(sentence, "text"));
        }

        return $"{string.Join(" ", texts)}\n\n{AgentReply.RequiredText(root, "limits_note")}";
    }

    private static object Measure(string id, string label, int numerator, int denominator, string of, bool suppress = false)
    {
        var suppressed = suppress || denominator < SmallCountThreshold || IsSmall(numerator) || IsSmall(denominator - numerator);
        return new
        {
            MetricId = id, Label = label, DenominatorLabel = of,
            Numerator = suppressed ? (int?)null : numerator,
            Denominator = suppressed ? (int?)null : denominator,
            Value = suppressed ? (int?)null : (int)Math.Round(100.0 * numerator / denominator, MidpointRounding.AwayFromZero),
            Unit = "%", MissingCount = (int?)null, Suppressed = suppressed,
            Comparison = new { Status = "not_tracked" },
        };
    }

    private static bool IsSmall(int count) => count is > 0 and < SmallCountThreshold;

    private static string StepWord(string key) => StepWords.TryGetValue(key, out var words) ? words : MessageFormat.Role(key);

    private static string ExceptionName(string key) =>
        Enum.TryParse<CaseExceptions>(key, out var exception) ? AgentWords.Exception(exception) : MessageFormat.Role(key);

    private static string ClosureName(string key) => ClosureWords.TryGetValue(key, out var words) ? words : MessageFormat.Role(key);

    /// <summary>An impact-summary prompt and the metric IDs it holds.</summary>
    /// <param name="Prompt">The prompt.</param>
    /// <param name="MetricIds">Every metric ID sent, which each sentence's <c>metric_ids</c> must come from.</param>
    internal sealed record ImpactInput(string Prompt, IReadOnlySet<string> MetricIds);
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
