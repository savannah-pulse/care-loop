using System.Text;
using CareLoop.Api.Features.Common;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Ai;
using CareLoop.Contracts.Common;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Features.Agents;

/// <summary>
/// Slice for <c>POST /ai/role-briefing</c>: asks the role-briefing agent for a few words on where the reader should
/// start today.
/// </summary>
/// <remarks>
/// <para>
/// The prompt is built from the same scope as the worklist reads: the reader's facility, or another facility with a
/// documented reason, and for the programme coordinator only the cases escalated to that role. It carries the reader's
/// role and facility, counts of the open cases in scope, and up to <see cref="MaxCases"/> cases in the order the
/// dashboard uses (overdue or escalated, then urgent, then due today, earliest deadline first). Each case is described
/// by its eight-character case number, programme, display step, next action, due status and exception flags only.
/// </para>
/// <para>
/// It never carries a patient's name, record number or reference, the hospital's result identifier, a staff member's
/// reference or any free text such as an escalation reason. The read is recorded like a worklist read, and what the
/// agent writes is a draft: it is not stored and changes nothing.
/// </para>
/// </remarks>
public static class DraftRoleBriefing
{
    /// <summary>The view key the read is recorded under; a refused read appends <c>-denied</c>.</summary>
    public const string AuditView = "role-briefing";

    /// <summary>The prompt's version, written into the prompt so a change of wording can be traced.</summary>
    public const string PromptVersion = "role-briefing-v1";

    /// <summary>Most cases the prompt describes one by one.</summary>
    public const int MaxCases = 5;

    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapDraftRoleBriefing(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/ai/role-briefing", Handle).WithName("DraftRoleBriefing").WithTags("Ai")
            .Produces<AiTextResponse>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return routes;
    }

    /// <summary>Asks the role-briefing agent to sum up the reader's worklist in a few plain words.</summary>
    /// <param name="facilityId">Facility to brief on; defaults to the staff reader's facility, and every facility for the coordinator.</param>
    /// <param name="reason">Reason required to brief on another facility's cases, recorded with the read.</param>
    /// <param name="context">The caller-stated staff identity.</param>
    /// <param name="db">The read context.</param>
    /// <param name="pathways">The display-step rules.</param>
    /// <param name="format">Gives local dates, times and facility names.</param>
    /// <param name="agents">Calls the agent.</param>
    /// <param name="options">Whether the agents are on, the agent's name and which urgency codes mean urgent.</param>
    /// <param name="clock">The current time, for deadlines and the read audit.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// 200 with the agent's text; 400, 401 or 403 as for the worklist reads; 501 while <c>Ai:Agents:Enabled</c> is
    /// <see langword="false"/>; 503 when the agent could not write it.
    /// </returns>
    public static async Task<IResult> Handle([FromQuery] string? facilityId, [FromQuery] string? reason, HttpContext context,
        CareLoopDbContext db, IPathwayProvider pathways, MessageFormat format, IAgentClient agents, IOptions<AgentOptions> options,
        IClock clock, CancellationToken ct)
    {
        if (ReadAccess.Identity(context, out var actor) is { } identityError) return identityError;
        if (ReadAccess.ValidateReason(reason) is { } reasonError) return reasonError;
        if (facilityId?.Length > 100 || facilityId is not null && string.IsNullOrWhiteSpace(facilityId))
            return Results.Problem("Invalid facility filter.", statusCode: 400);
        if (!options.Value.Enabled) return AgentReplies.Disabled("RoleBriefing");

        var scopeFacility = facilityId ?? (actor.Role == DemoRoles.Coordinator ? null : actor.FacilityId);
        var queryText = ReadAccess.Query(("facilityId", scopeFacility));
        var outside = actor.Role != DemoRoles.Coordinator && facilityId is not null && facilityId != actor.FacilityId;
        if (ReadAccess.CrossFacilityPermission(actor, facilityId, reason) is { } denied)
        {
            await ReadAccess.AuditAsync(db, actor, clock, AuditView + "-denied", null, outside, reason, queryText, ct);
            return denied;
        }

        var cases = await ReadAccess.FacilityScope(ReadAccess.RoleScope(db.Cases.AsNoTracking(), actor), actor, facilityId)
            .Where(c => c.Stage != CaseStage.Closed).ToListAsync(ct);
        await ReadAccess.AuditAsync(db, actor, clock, AuditView, null, outside, reason, queryText, ct);
        var prompt = BuildPrompt(actor, scopeFacility, outside, cases, pathways, options.Value.UrgentCodes, format, clock.UtcNow);
        return await AgentReplies.AskAsync(agents, options.Value.RoleBriefing, prompt, clock, ct);
    }

    /// <summary>
    /// Writes the role-briefing prompt.
    /// </summary>
    /// <param name="reader">Who asked; only the role is used, and whether a case is theirs.</param>
    /// <param name="facilityId">The facility in scope, or <see langword="null"/> for every facility.</param>
    /// <param name="outsideTeam">Whether the reader is looking at another team's cases.</param>
    /// <param name="cases">The open cases in scope.</param>
    /// <param name="pathways">The display-step rules.</param>
    /// <param name="urgentCodes">Urgency codes that mean urgent.</param>
    /// <param name="format">Gives local dates, times and facility names.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The plain-text prompt.</returns>
    internal static string BuildPrompt(Actor reader, string? facilityId, bool outsideTeam, IReadOnlyList<Case> cases,
        IPathwayProvider pathways, IReadOnlyCollection<string> urgentCodes, MessageFormat format, DateTimeOffset now)
    {
        var urgent = new HashSet<string>(urgentCodes, StringComparer.OrdinalIgnoreCase);
        bool IsUrgent(Case c) => c.Urgency is not null && urgent.Contains(c.Urgency);
        bool IsOverdue(Case c) => c.DueAt < now || c.Exceptions.HasFlag(CaseExceptions.Overdue);
        bool IsDueToday(Case c) => !IsOverdue(c) && c.DueAt is { } due && AgentWords.IsSameLocalDay(due, now, format);
        int? Priority(Case c) => IsOverdue(c) || c.IsEscalated ? 0 : IsUrgent(c) ? 1 : IsDueToday(c) ? 2 : null;

        var prompt = new StringBuilder();
        prompt.AppendLine($"CareLoop role briefing, prompt version {PromptVersion}.");
        prompt.AppendLine("Write one to three short sentences that tell the reader where to start today, using only the facts below.");
        prompt.AppendLine("Use plain words and British spelling. Do not invent cases, numbers, names or deadlines. Do not give clinical advice: people make every decision in CareLoop.");
        prompt.AppendLine();
        prompt.AppendLine($"Reader: {AgentWords.Role(reader.Role)}.");
        prompt.AppendLine($"Facility: {Scope(reader, facilityId, outsideTeam, format)}");
        prompt.AppendLine($"Now: {AgentWords.Moment(now, format)}, local time.");
        prompt.AppendLine();
        prompt.AppendLine($"Open cases: {cases.Count}");
        prompt.AppendLine($"Overdue: {cases.Count(IsOverdue)}");
        prompt.AppendLine($"Escalated: {cases.Count(c => c.IsEscalated)}");
        prompt.AppendLine($"Urgent: {cases.Count(IsUrgent)}");
        prompt.AppendLine($"Due today: {cases.Count(IsDueToday)}");
        prompt.AppendLine($"Needs an owner: {cases.Count(c => c.Exceptions.HasFlag(CaseExceptions.Unassigned))}");
        prompt.AppendLine();

        var first = cases.Select(c => (Case: c, Priority: Priority(c))).Where(p => p.Priority is not null)
            .OrderBy(p => p.Priority).ThenBy(p => p.Case.DueAt ?? DateTimeOffset.MaxValue).ThenBy(p => p.Case.Id)
            .Take(MaxCases).Select(p => p.Case).ToList();
        if (first.Count == 0)
        {
            prompt.AppendLine("Cases to start with: none. No open case is overdue, escalated, urgent or due today.");
            return prompt.ToString();
        }

        prompt.AppendLine("Cases to start with, most pressing first:");
        for (var i = 0; i < first.Count; i++)
        {
            var c = first[i];
            var flags = AgentWords.Flags(c).ToList();
            if (IsUrgent(c)) flags.Insert(0, "urgent");
            prompt.AppendLine(string.Join(" ",
                $"{i + 1}. Case {NotificationRouter.CaseReferenceFor(c.Id)}.",
                $"Programme: {AgentWords.Programme(c.PathwayId)}.",
                $"Step: {AgentWords.Step(CaseDisplayLabels.From(c, pathways.Get(c.PathwayId)))}.",
                $"Next action: {AgentWords.NextAction(c.NextAction)}.",
                $"Due: {AgentWords.Due(c.DueAt, now, format)}.",
                $"Flags: {(flags.Count == 0 ? "none" : string.Join(", ", flags))}.",
                $"Acts next: {ActsNext(c.Owner, reader)}."));
        }

        return prompt.ToString();
    }

    private static string Scope(Actor reader, string? facilityId, bool outsideTeam, MessageFormat format)
    {
        var place = facilityId is null ? "every facility" : format.Facility(facilityId);
        if (reader.Role == DemoRoles.Coordinator)
        {
            return $"{place}; only the cases escalated to the programme coordinator.";
        }

        return outsideTeam ? $"{place}, another team's cases." : $"{place}, the reader's team.";
    }

    private static string ActsNext(CaseOwner owner, Actor reader)
    {
        if (!owner.IsQueue && string.Equals(WithoutType(owner.Reference), WithoutType(reader.Reference), StringComparison.OrdinalIgnoreCase))
        {
            return "the reader";
        }

        var role = AgentWords.Role(owner.Role);
        return owner.IsQueue ? $"the {role} queue" : $"a {role}";
    }

    // Staff are stored without a FHIR type ("rekha-varghese") but a caller may state one ("Practitioner/rekha-varghese").
    private static string WithoutType(string reference)
    {
        var trimmed = reference.Trim();
        var slash = trimmed.IndexOf('/', StringComparison.Ordinal);
        return slash > 0 && trimmed[..slash].All(char.IsAsciiLetter) ? trimmed[(slash + 1)..] : trimmed;
    }
}
