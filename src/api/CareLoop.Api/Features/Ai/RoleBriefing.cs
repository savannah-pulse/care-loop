using System.Text.RegularExpressions;
using CareLoop.Api.Features.Common;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Ai;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Ai;

/// <summary>
/// <c>GET /ai/role-briefing</c>: a short AI summary, for the home page, of the open cases the signed-in person can see.
/// </summary>
/// <remarks>
/// The data is the reader's own worklist scope, the same cases <c>GET /cases</c> gives them: counts, and the most
/// pressing cases with their step, next action and deadline. It carries no patient name, record number, reference,
/// contact detail, case number, staff name or free text: the model knows a case only as a token such as <c>C-1</c>. The
/// API writes the opening sentence, and afterwards puts each patient's name where the text names a token, as the reader
/// already sees it in the list (the case number for a reader who may not see names). The wording of the summary is in <c>Prompts/role-briefing.md</c>.
/// </remarks>
public static partial class RoleBriefing
{
    /// <summary>Most cases described one by one; the counts still cover every case.</summary>
    public const int MaxCasesListed = 25;

    /// <summary>Maps the route.</summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapRoleBriefing(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/ai/role-briefing", Handle).WithName("GetRoleBriefing").WithTags("Ai")
            .Produces<AiTextResponse>().ProducesProblem(401).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return routes;
    }

    internal static async Task<IResult> Handle(HttpContext context, CareLoopDbContext db, IPathwayProvider pathways, MessageFormat format,
        AiText ai, IClock clock, CancellationToken ct, [FromQuery] bool refresh = false)
    {
        if (ReadAccess.Identity(context, out var actor) is { } error) return error;
        if (!ai.IsOn) return AiText.SwitchedOff();

        var cases = Order(await ReadAccess.FacilityScope(ReadAccess.RoleScope(db.Cases.AsNoTracking(), actor), actor, null)
            .Where(c => c.Stage != CaseStage.Closed).ToListAsync(ct), clock.UtcNow);
        await ReadAccess.AuditAsync(db, actor, clock, "role-briefing", null, false, null, null, ct);
        var opening = Opening(cases, format, clock.UtcNow);
        // The model knows each case only as C-1, C-2, …; who each one is goes in here and never leaves CareLoop.
        return await ai.WriteAsync(AiPrompts.RoleBriefing, BuildData(actor, cases, opening, pathways, format, clock.UtcNow), refresh, reuse: true, ct,
            text => $"{opening} {WithCaseNames(text, cases, actor)}");
    }

    /// <summary>The briefing's first sentence, written by the API so its counts are always right.</summary>
    /// <param name="cases">The open cases in the reader's scope.</param>
    /// <param name="format">Local dates.</param>
    /// <param name="now">The time now.</param>
    /// <returns>For example <c>4 cases are overdue and 1 is due today.</c></returns>
    internal static string Opening(IReadOnlyList<Case> cases, MessageFormat format, DateTimeOffset now)
    {
        int overdue = cases.Count(c => IsOverdue(c, now)), today = cases.Count(c => IsDueToday(c, format, now));
        static string Count(int n) => n == 1 ? "1 case is" : $"{n} cases are";
        return overdue == 0 && today == 0 ? "Nothing is overdue or due today." : $"{Count(overdue)} overdue and {(today == 1 ? "1 is" : $"{today} are")} due today.";
    }

    /// <summary>Says who a case is where the text names it by its token.</summary>
    /// <param name="text">The model's text, naming cases as <c>{{C-1}}</c> or <c>C-1</c>.</param>
    /// <param name="cases">The cases, in the order their tokens were given.</param>
    /// <param name="reader">Who is reading; the programme coordinator is never shown a patient's name.</param>
    /// <returns>
    /// The text with each known token replaced by the patient's name, or by <c>case</c> and its number for a reader who may
    /// not see names or a case with no name on record; an unknown token is left as it is.
    /// </returns>
    /// <remarks>The name is the one the reader already sees in the list below. It is added here, after the answer, so it is never sent.</remarks>
    internal static string WithCaseNames(string text, IReadOnlyList<Case> cases, Actor reader) =>
        Token().Replace(text, match =>
        {
            if (!int.TryParse(match.Groups[1].Value, out var n) || n < 1 || n > Math.Min(cases.Count, MaxCasesListed))
            {
                return match.Value;
            }

            var named = cases[n - 1];
            return reader.Role != DemoRoles.Coordinator && !string.IsNullOrWhiteSpace(named.PatientDisplayName)
                ? named.PatientDisplayName
                : $"case {NotificationRouter.CaseReferenceFor(named.Id)}";
        });

    private static bool IsOverdue(Case c, DateTimeOffset now) => c.DueAt < now || c.Exceptions.HasFlag(CaseExceptions.Overdue);

    private static bool IsDueToday(Case c, MessageFormat format, DateTimeOffset now) =>
        !IsOverdue(c, now) && c.DueAt is { } due && format.Date(due) == format.Date(now);

    // Most pressing first; the tokens follow this order.
    private static List<Case> Order(List<Case> cases, DateTimeOffset now) =>
        cases.OrderByDescending(c => IsOverdue(c, now)).ThenByDescending(c => c.IsEscalated).ThenBy(c => c.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(c => c.Id).ToList();

    /// <summary>Builds what the model is given: the opening, counts over every case, and the most pressing cases one by one.</summary>
    /// <param name="reader">Who is reading.</param>
    /// <param name="cases">The open cases in the reader's scope, most pressing first.</param>
    /// <param name="opening">The first sentence, which the model continues from.</param>
    /// <param name="pathways">Gives each case's step.</param>
    /// <param name="format">Local dates and times.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The data, with nothing that identifies a patient, a case or a member of staff: a case is only a token.</returns>
    internal static object BuildData(Actor reader, IReadOnlyList<Case> cases, string opening, IPathwayProvider pathways, MessageFormat format, DateTimeOffset now) => new
    {
        Opening = opening,
        ReaderRole = MessageFormat.Role(reader.Role),
        Today = format.Date(now),
        OpenCases = cases.Count,
        Overdue = cases.Count(c => IsOverdue(c, now)),
        DueToday = cases.Count(c => IsDueToday(c, format, now)),
        Escalated = cases.Count(c => c.IsEscalated),
        WithoutOwner = cases.Count(c => c.Exceptions.HasFlag(CaseExceptions.Unassigned)),
        Cases = cases.Take(MaxCasesListed).Select((c, i) => new
        {
            Token = $"C-{i + 1}",
            Step = Words(CaseDisplayLabels.From(c, pathways.Get(c.PathwayId)).ToString()),
            NextAction = c.NextAction is null ? "none" : MessageFormat.Role(c.NextAction),
            Deadline = c.DueAt is { } due ? $"{format.Day(due)} {format.Time(due)}" : "none",
            Status = IsOverdue(c, now) ? "overdue" : IsDueToday(c, format, now) ? "due today" : "upcoming",
            // Supplied so the model never has to work a duration out.
            OverdueBy = c.DueAt is { } late && late < now ? Duration(now - late) : null,
            Escalated = c.IsEscalated,
            NeedsOwner = c.Exceptions.HasFlag(CaseExceptions.Unassigned),
            HeldBy = c.Owner.IsQueue ? $"{MessageFormat.Role(c.Owner.Role)} queue" : MessageFormat.Role(c.Owner.Role),
            Yours = !c.Owner.IsQueue && string.Equals(c.Owner.Reference, reader.Reference, StringComparison.OrdinalIgnoreCase),
            PatientMessage = c.Notification is null ? "not sent" : Words(c.Notification.Status.ToString()),
        }).ToList(),
    };

    /// <summary>Writes a duration in words, to the largest two units.</summary>
    /// <param name="span">The duration.</param>
    /// <returns>For example <c>1 day 21 hours</c>, <c>18 hours</c> or <c>40 minutes</c>.</returns>
    internal static string Duration(TimeSpan span)
    {
        static string Unit(int n, string unit) => n == 1 ? $"1 {unit}" : $"{n} {unit}s";
        return span.TotalDays >= 1 ? $"{Unit((int)span.TotalDays, "day")} {Unit(span.Hours, "hour")}"
            : span.TotalHours >= 1 ? Unit(span.Hours, "hour")
            : Unit(Math.Max(1, span.Minutes), "minute");
    }

    /// <summary>Turns a code such as <c>PatientNotified</c> into words: <c>patient notified</c>.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The words, in lower case.</returns>
    internal static string Words(string code) => Capital().Replace(code, " $1").Trim().ToLowerInvariant();

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex Capital();

    [GeneratedRegex(@"\{*\bC-(\d+)\b\}*")]
    private static partial Regex Token();
}
