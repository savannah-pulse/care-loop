using System.Text.RegularExpressions;
using CareLoop.Api.Features.Common;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Ai;
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
/// contact detail, case number, staff name or free text. The wording of the summary is in <c>Prompts/role-briefing.md</c>.
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

        var cases = await ReadAccess.FacilityScope(ReadAccess.RoleScope(db.Cases.AsNoTracking(), actor), actor, null)
            .Where(c => c.Stage != CaseStage.Closed).ToListAsync(ct);
        await ReadAccess.AuditAsync(db, actor, clock, "role-briefing", null, false, null, null, ct);
        return await ai.WriteAsync(AiPrompts.RoleBriefing, BuildData(actor, cases, pathways, format, clock.UtcNow), refresh, reuse: true, ct);
    }

    /// <summary>Builds what the model is given: counts over every case, and the most pressing cases one by one.</summary>
    /// <param name="reader">Who is reading.</param>
    /// <param name="cases">The open cases in the reader's scope.</param>
    /// <param name="pathways">Gives each case's step.</param>
    /// <param name="format">Local dates and times.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The data, with nothing that identifies a patient, a case or a member of staff.</returns>
    internal static object BuildData(Actor reader, IReadOnlyList<Case> cases, IPathwayProvider pathways, MessageFormat format, DateTimeOffset now)
    {
        bool Overdue(Case c) => c.DueAt < now || c.Exceptions.HasFlag(CaseExceptions.Overdue);
        bool DueToday(Case c) => !Overdue(c) && c.DueAt is { } due && format.Date(due) == format.Date(now);
        return new
        {
            ReaderRole = MessageFormat.Role(reader.Role),
            Today = format.Date(now),
            OpenCases = cases.Count,
            Overdue = cases.Count(Overdue),
            DueToday = cases.Count(DueToday),
            Escalated = cases.Count(c => c.IsEscalated),
            WithoutOwner = cases.Count(c => c.Exceptions.HasFlag(CaseExceptions.Unassigned)),
            Cases = cases.OrderByDescending(Overdue).ThenByDescending(c => c.IsEscalated).ThenBy(c => c.DueAt ?? DateTimeOffset.MaxValue)
                .Take(MaxCasesListed)
                .Select(c => new
                {
                    Step = Words(CaseDisplayLabels.From(c, pathways.Get(c.PathwayId)).ToString()),
                    NextAction = c.NextAction is null ? "none" : MessageFormat.Role(c.NextAction),
                    Deadline = c.DueAt is { } due ? $"{format.Day(due)} {format.Time(due)}" : "none",
                    Status = Overdue(c) ? "overdue" : DueToday(c) ? "due today" : "upcoming",
                    Escalated = c.IsEscalated,
                    NeedsOwner = c.Exceptions.HasFlag(CaseExceptions.Unassigned),
                    HeldBy = c.Owner.IsQueue ? $"{MessageFormat.Role(c.Owner.Role)} queue" : MessageFormat.Role(c.Owner.Role),
                    Yours = !c.Owner.IsQueue && string.Equals(c.Owner.Reference, reader.Reference, StringComparison.OrdinalIgnoreCase),
                    PatientMessage = c.Notification is null ? "not sent" : Words(c.Notification.Status.ToString()),
                }).ToList(),
        };
    }

    /// <summary>Turns a code such as <c>PatientNotified</c> into words: <c>patient notified</c>.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The words, in lower case.</returns>
    internal static string Words(string code) => Capital().Replace(code, " $1").Trim().ToLowerInvariant();

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex Capital();
}
