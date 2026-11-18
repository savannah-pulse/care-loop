using System.Text;
using System.Text.Json;
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
using CareLoop.Domain.Tracks;
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
/// dashboard uses (overdue or escalated, then urgent, then due today, earliest deadline first).
/// </para>
/// <para>
/// Each of those cases is described from its full coordination state, the data <c>GET /cases/{caseId}</c> returns,
/// not the worklist row: its eight-character case number, programme, stage and display step, next action, owner role
/// and who acts next, due and overdue state, urgency, exception flags, the open escalation's level and whether it was
/// acknowledged, each track's status, how contact with the patient stands, understanding and the confidence rating
/// where recorded, and the patient's preferred language for messages, read from the stored screening result.
/// </para>
/// <para>
/// It never carries a patient's name, record number, reference or contact details, the hospital's result or record
/// identifiers, a staff member's reference, or any free text from the hospital system, such as an escalation reason, a
/// referral's rejection reason or an appointment's place. The read is recorded like a worklist read, with the agent
/// version, prompt version and correlation identifier, and what the agent writes is a draft: it is not stored and
/// changes nothing.
/// </para>
/// </remarks>
public static partial class DraftRoleBriefing
{
    /// <summary>The view key the read is recorded under; a refused read appends <c>-denied</c>.</summary>
    public const string AuditView = "role-briefing";

    /// <summary>What the text is for, as the request to the AI provider names it.</summary>
    public const string Purpose = "role_briefing";

    /// <summary>The prompt's version, written into the prompt and returned with the text so a change of wording can be traced.</summary>
    public const string PromptVersion = "role-briefing-v2";

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
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return routes;
    }

    /// <summary>Asks the role-briefing agent to sum up the reader's worklist in a few plain words.</summary>
    /// <param name="facilityId">Facility to brief on; defaults to the staff reader's facility, and every facility for the coordinator.</param>
    /// <param name="reason">Reason required to brief on another facility's cases, recorded with the read.</param>
    /// <param name="context">The caller-stated staff identity.</param>
    /// <param name="db">The read context.</param>
    /// <param name="pathways">The display-step rules.</param>
    /// <param name="format">Gives local dates, times and facility names.</param>
    /// <param name="agents">Calls the agent on the route <c>Ai:Provider</c> chose.</param>
    /// <param name="options">Whether the agents are on, the agent version and which urgency codes mean urgent.</param>
    /// <param name="clock">The current time, for deadlines and the read audit.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// 200 with the agent's text; 400, 401 or 403 as for the worklist reads; 409 while the agents are switched off
    /// (<c>Ai:Agents:Enabled</c> is <see langword="false"/> or <c>Ai:Provider</c> is <c>NoOp</c>); 503 when the agent
    /// could not write it.
    /// </returns>
    public static async Task<IResult> Handle([FromQuery] string? facilityId, [FromQuery] string? reason, HttpContext context,
        CareLoopDbContext db, IPathwayProvider pathways, MessageFormat format, IAgentClient agents, IOptions<AgentOptions> options,
        IClock clock, CancellationToken ct)
    {
        if (ReadAccess.Identity(context, out var actor) is { } identityError) return identityError;
        if (ReadAccess.ValidateReason(reason) is { } reasonError) return reasonError;
        if (facilityId?.Length > 100 || facilityId is not null && string.IsNullOrWhiteSpace(facilityId))
            return Results.Problem("Invalid facility filter.", statusCode: 400);
        if (!AgentReplies.IsOn(options.Value, agents)) return AgentReplies.SwitchedOff("RoleBriefing", options.Value);

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
        var urgentCodes = options.Value.UrgentCodes;
        var first = StartWith(cases, urgentCodes, format, clock.UtcNow);
        var languages = await PreferredLanguagesAsync(db, first.Select(c => c.Id).ToList(), ct);
        var request = new AgentRequest
        {
            Purpose = Purpose, Agent = options.Value.RoleBriefingAgent(), PromptVersion = PromptVersion,
            Prompt = BuildPrompt(actor, scopeFacility, outside, cases, pathways, urgentCodes, format, clock.UtcNow, languages),
            CorrelationId = Guid.NewGuid(),
        };
        await AgentReplies.AuditAsync(db, actor, clock, AuditView, null, outside, reason, queryText, request, ct);
        return await AgentReplies.AskAsync(agents, request, clock, ct);
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
    /// <param name="languages">The patient's preferred language for messages, by case, for the cases described; a case missing from it has none recorded.</param>
    /// <returns>The plain-text prompt. It holds no name, identifier, contact detail, staff reference or free text.</returns>
    internal static string BuildPrompt(Actor reader, string? facilityId, bool outsideTeam, IReadOnlyList<Case> cases,
        IPathwayProvider pathways, IReadOnlyCollection<string> urgentCodes, MessageFormat format, DateTimeOffset now,
        IReadOnlyDictionary<Guid, string>? languages = null)
    {
        var urgent = new HashSet<string>(urgentCodes, StringComparer.OrdinalIgnoreCase);
        bool IsUrgent(Case c) => c.Urgency is not null && urgent.Contains(c.Urgency);

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
        prompt.AppendLine($"Overdue: {cases.Count(c => IsOverdue(c, now))}");
        prompt.AppendLine($"Escalated: {cases.Count(c => c.IsEscalated)}");
        prompt.AppendLine($"Urgent: {cases.Count(IsUrgent)}");
        prompt.AppendLine($"Due today: {cases.Count(c => IsDueToday(c, format, now))}");
        prompt.AppendLine($"Needs an owner: {cases.Count(c => c.Exceptions.HasFlag(CaseExceptions.Unassigned))}");
        prompt.AppendLine();

        var first = StartWith(cases, urgentCodes, format, now);
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
            foreach (var line in Details(c, IsUrgent(c), format, now, languages))
            {
                prompt.AppendLine($"   {line}");
            }
        }

        return prompt.ToString();
    }

    /// <summary>
    /// Picks the cases the briefing describes one by one.
    /// </summary>
    /// <param name="cases">The open cases in scope.</param>
    /// <param name="urgentCodes">Urgency codes that mean urgent.</param>
    /// <param name="format">Supplies the local day.</param>
    /// <param name="now">The current time.</param>
    /// <returns>Up to <see cref="MaxCases"/> cases: overdue or escalated, then urgent, then due today; earliest deadline first, then by identifier.</returns>
    internal static List<Case> StartWith(IReadOnlyList<Case> cases, IReadOnlyCollection<string> urgentCodes, MessageFormat format, DateTimeOffset now)
    {
        var urgent = new HashSet<string>(urgentCodes, StringComparer.OrdinalIgnoreCase);
        int? Priority(Case c) =>
            IsOverdue(c, now) || c.IsEscalated ? 0
            : c.Urgency is not null && urgent.Contains(c.Urgency) ? 1
            : IsDueToday(c, format, now) ? 2
            : null;
        return cases.Select(c => (Case: c, Priority: Priority(c))).Where(p => p.Priority is not null)
            .OrderBy(p => p.Priority).ThenBy(p => p.Case.DueAt ?? DateTimeOffset.MaxValue).ThenBy(p => p.Case.Id)
            .Take(MaxCases).Select(p => p.Case).ToList();
    }

    /// <summary>
    /// Reads each case's preferred language for messages from its latest applied screening result, where CareLoop keeps it.
    /// </summary>
    /// <param name="db">The read context.</param>
    /// <param name="caseIds">The cases described.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The language tag by case; a case whose tag is missing or not a well-formed BCP 47 tag is left out.</returns>
    /// <remarks>
    /// The hospital system sends the patient's preferred language with the screening result (FHIR
    /// <c>Patient.communication</c>); CareLoop stores it in that event to choose the language of patient templates, not on
    /// the case. Only the tag is read: the rest of the payload, contact details included, never leaves this method.
    /// </remarks>
    private static async Task<IReadOnlyDictionary<Guid, string>> PreferredLanguagesAsync(CareLoopDbContext db, IReadOnlyList<Guid> caseIds, CancellationToken ct)
    {
        if (caseIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var results = await db.SourceEvents.AsNoTracking()
            .Where(e => caseIds.Contains(e.CaseId)
                && e.Type == CaseEventType.ScreeningResultReceived && e.Disposition == SourceEventDisposition.Applied)
            .Select(e => new { e.CaseId, e.ReceivedAt, e.PayloadJson })
            .ToListAsync(ct);
        var languages = new Dictionary<Guid, string>();
        foreach (var latest in results.GroupBy(e => e.CaseId).Select(g => g.OrderByDescending(e => e.ReceivedAt).First()))
        {
            using var payload = JsonDocument.Parse(latest.PayloadJson);
            if (payload.RootElement.ValueKind == JsonValueKind.Object
                && payload.RootElement.TryGetProperty("patientLanguage", out var tag) && tag.ValueKind == JsonValueKind.String
                && tag.GetString() is { } value && LanguageTag().IsMatch(value.Trim()))
            {
                languages[latest.CaseId] = value.Trim();
            }
        }

        return languages;
    }

    private static IEnumerable<string> Details(Case c, bool urgent, MessageFormat format, DateTimeOffset now, IReadOnlyDictionary<Guid, string>? languages)
    {
        var due = c.DueAt is null ? "no deadline" : IsOverdue(c, now) ? "overdue" : "not overdue";
        yield return $"Stage: {AgentWords.Stage(c.Stage)}. Owner role: {AgentWords.Owner(c.Owner)}. Due state: {due}. Urgency: {(c.Urgency is null ? "not reviewed yet" : urgent ? "urgent" : "not urgent")}.";
        yield return $"Escalation: {Escalation(c)}.";
        yield return $"Tracks: notification {AgentWords.Track(c.Notification?.Status)}; consultation {AgentWords.Track(c.Consultation?.Status)}; referral {AgentWords.Track(c.Referral?.Status)}; appointment {Appointment(c.Appointment, format)}.";
        yield return $"Patient contact: {Contact(c)}.";
        if (c.Notification is { Status: NotificationStatus.Understood })
        {
            yield return "Understanding: the patient's understanding of the next step is recorded.";
        }

        if (c.Notification?.Confidence is { } confidence)
        {
            yield return $"Confidence after a call: {confidence} out of 5.";
        }

        if (languages is not null && languages.TryGetValue(c.Id, out var language))
        {
            yield return $"Preferred language for messages: {AgentWords.Language(language)}.";
        }
    }

    private static string Escalation(Case c)
    {
        var open = c.Escalations.LastOrDefault(e => e.IsOpen);
        if (open is null)
        {
            return "none open";
        }

        var to = AgentWords.Role(open.EscalatedTo.Role);
        return open.IsAcknowledged
            ? $"level {open.Level}, with the {to}, acknowledged"
            : $"level {open.Level}, with the {to}, not acknowledged yet";
    }

    private static string Appointment(AppointmentTrack? appointment, MessageFormat format) => appointment switch
    {
        null => "not started",
        { Status: AppointmentStatus.Scheduled, ScheduledFor: { } at } => $"booked for {format.Date(at)} at {format.Time(at)}",
        _ => AgentWords.Track(appointment.Status),
    };

    private static string Contact(Case c)
    {
        var parts = new List<string>
        {
            c.Notification?.Status switch
            {
                null => "no message sent yet",
                NotificationStatus.Sent => "message sent, delivery not confirmed",
                NotificationStatus.Delivered => "message delivered, no reply yet",
                NotificationStatus.Failed => "message not delivered on any channel",
                NotificationStatus.Acknowledged => "the patient replied that they got the message",
                _ => "the patient replied that they got the message, and their understanding was checked",
            },
        };
        if (c.Notification is { CallRequested: true })
        {
            parts.Add("the patient asked for a call");
        }

        if (c.Exceptions.HasFlag(CaseExceptions.Unreachable))
        {
            parts.Add("not reached");
        }

        return string.Join("; ", parts);
    }

    private static bool IsOverdue(Case c, DateTimeOffset now) => c.DueAt < now || c.Exceptions.HasFlag(CaseExceptions.Overdue);

    private static bool IsDueToday(Case c, MessageFormat format, DateTimeOffset now) =>
        !IsOverdue(c, now) && c.DueAt is { } due && AgentWords.IsSameLocalDay(due, now, format);

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

    [GeneratedRegex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{1,8})*$")]
    private static partial Regex LanguageTag();
}
