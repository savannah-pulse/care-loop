using CareLoop.Api.Features.Common;
using CareLoop.Api.Features.Metrics;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Ai;
using CareLoop.Contracts.Metrics;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Features.Ai;

/// <summary>
/// <c>GET /ai/impact-summary</c>: a short AI reading, for the impact page, of the numbers <c>GET /metrics</c> returns.
/// </summary>
/// <remarks>
/// The numbers are read through <see cref="GetMetrics"/>, so the access rules, the audit and the figures are the same
/// as the page's. Only totals are sent, never a case. A count from 1 to <see cref="SmallCount"/> minus 1 is withheld,
/// because a very small group can point at a person. The wording of the summary is in <c>Prompts/impact-summary.md</c>.
/// </remarks>
public static class ImpactSummary
{
    /// <summary>Counts above zero and below this are not sent. A placeholder until the team sets the threshold.</summary>
    public const int SmallCount = 5;

    /// <summary>The sentence the API puts at the end of the summary while the data is synthetic.</summary>
    public const string SyntheticNote = "Simulated on synthetic data; not evidence of real-world improvement.";

    /// <summary>What a value too small to send is replaced with.</summary>
    public const string Withheld = "withheld";

    /// <summary>Maps the route.</summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapImpactSummary(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/ai/impact-summary", Handle).WithName("GetImpactSummary").WithTags("Ai")
            .Produces<AiTextResponse>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return routes;
    }

    internal static async Task<IResult> Handle([FromQuery] string? facilityId, [FromQuery] string? reason, HttpContext context,
        CareLoopDbContext db, IPathwayProvider pathways, MessageFormat format, AiText ai, IOptions<AiOptions> options, IClock clock,
        ILoggerFactory loggerFactory, CancellationToken ct, [FromQuery] bool refresh = false)
    {
        if (ReadAccess.Identity(context, out var actor) is { } error) return error;
        if (!ai.IsOn) return AiText.SwitchedOff();

        // The same read as the impact page: same checks, same audit entry, same numbers.
        var read = await GetMetrics.Handle(facilityId, reason, context, db, pathways, clock, loggerFactory, ct);
        if (read is not IValueHttpResult { Value: MetricsResponse metrics }) return read;

        var facility = facilityId ?? (actor.Role == DemoRoles.Coordinator ? null : actor.FacilityId);
        // Fixed text is the API's, not the model's: while the data is synthetic, the summary always ends by saying so.
        return await ai.WriteAsync(AiPrompts.ImpactSummary, BuildData(metrics, facility is null ? "every facility" : format.Facility(facility), format),
            refresh, reuse: true, ct, options.Value.SyntheticData ? text => $"{text}\n\n{SyntheticNote}" : null);
    }

    /// <summary>Builds what the model is given from the page's numbers, with small counts withheld.</summary>
    /// <param name="metrics">The numbers the impact page shows.</param>
    /// <param name="facility">The facility the numbers cover, in words.</param>
    /// <param name="format">Local dates.</param>
    /// <returns>Totals, shares, median times and reasons; never a case. A withheld value is the word <c>withheld</c>.</returns>
    internal static object BuildData(MetricsResponse metrics, string facility, MessageFormat format)
    {
        // Total = open + closed, so when one is small the other two would give it away.
        var hideTotals = IsSmall(metrics.OpenCases) || IsSmall(metrics.ClosedCases) || IsSmall(metrics.TotalCases);
        // The closure reasons add up to the closed cases, so one small reason hides them all.
        var closures = metrics.ClosuresByReason.Where(c => c.Value > 0).ToList();
        return new
        {
            Facility = facility,
            Period = $"Cases opened up to {format.Date(metrics.GeneratedAt)}",
            TotalCases = hideTotals ? (object)Withheld : metrics.TotalCases,
            OpenCases = hideTotals ? (object)Withheld : metrics.OpenCases,
            ClosedCases = hideTotals ? (object)Withheld : metrics.ClosedCases,
            Shares = new Dictionary<string, object>
            {
                ["open cases past their deadline"] = Share(metrics.OverdueCases, metrics.OpenCases),
                ["open cases escalated"] = Share(metrics.EscalatedCases, metrics.OpenCases),
                ["open cases without an owner"] = Share(metrics.UnassignedCases, metrics.OpenCases),
            }.Concat(metrics.Measures.Where(m => m.Value.Denominator > 0)
                    .Select(m => KeyValuePair.Create(RoleBriefing.Words(Pascal(m.Key)), Share(m.Value.Numerator, m.Value.Denominator))))
                .ToDictionary(),
            MedianTimeFromResultTo = metrics.TimeToStep.Where(s => s.Value.Median is not null)
                .ToDictionary(s => RoleBriefing.Words(Pascal(s.Key)),
                    s => s.Value.Denominator < SmallCount ? (object)Withheld : new { Time = Duration(s.Value.Median!.Value), Cases = s.Value.Denominator }),
            OpenCasesWaitingBecause = metrics.CasesByException.Where(e => e.Value > 0)
                .ToDictionary(e => RoleBriefing.Words(e.Key), e => IsSmall(e.Value) ? (object)Withheld : e.Value),
            ClosedCasesByReason = closures.Any(c => IsSmall(c.Value))
                ? (object)Withheld
                : closures.ToDictionary(c => RoleBriefing.Words(c.Key), c => c.Value),
        };
    }

    private static bool IsSmall(int count) => count is > 0 and < SmallCount;

    // A share is withheld when it rests on few cases, or when either side of it is a small group.
    private static object Share(int numerator, int denominator) =>
        denominator < SmallCount || IsSmall(numerator) || IsSmall(denominator - numerator)
            ? Withheld
            : new { Numerator = numerator, Denominator = denominator, Percent = (int)Math.Round(100.0 * numerator / denominator) };

    private static string Pascal(string key) => key.Length == 0 ? key : char.ToUpperInvariant(key[0]) + key[1..];

    // Under a minute in seconds, as the page shows it, so a step of 8 seconds is never "1 minute".
    private static string Duration(TimeSpan span) =>
        span.TotalDays >= 1 ? $"{Count((int)span.TotalDays, "day")} {Count(span.Hours, "hour")}"
        : span.TotalHours >= 1 ? $"{Count(span.Hours, "hour")} {Count(span.Minutes, "minute")}"
        : span.TotalMinutes >= 1 ? Count(span.Minutes, "minute")
        : Count(Math.Max(1, span.Seconds), "second");

    private static string Count(int n, string unit) => n == 1 ? $"1 {unit}" : $"{n} {unit}s";
}
