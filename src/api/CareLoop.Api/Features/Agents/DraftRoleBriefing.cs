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
/// The agent (AG1 in the Foundry project) reads a worklist the backend has already scoped, banded and ordered, inside
/// <c>&lt;input_json&gt;</c>: the reader's role, the time, the working-day calendar and clock configuration, counts by
/// band and one item per case. The scope is the worklist's: the reader's facility, or another facility with a documented
/// reason, and for the programme coordinator only the cases escalated to that role. Bands: <c>P1</c> overdue or
/// escalated; <c>P2</c> due today, or on a non-working day before the next working day; <c>info</c> urgent tier or
/// needing an owner; <c>P3</c> due on one of the next <see cref="LookaheadDays"/> working days, a deadline on a
/// non-working day counting on the working day before it. Each item carries the next action, the step, the deadline
/// and the working days left, the urgency tier, whether the reader acts next, how contact with the patient stands and
/// any open escalation.
/// </para>
/// <para>
/// A case is named only by a token made up for this one prompt (<c>P-1</c>, <c>P-2</c>…). The agent answers JSON: a
/// headline and sections that name items by token. The API checks that every item it lists was sent and that every P1
/// item is listed or counted, then puts the case number back in place of each token, as the dashboard shows it. A
/// refusal (<c>cannot_comply</c>) or an answer that breaks those checks is the calm 503, with the reason code in the log.
/// </para>
/// <para>
/// The prompt never carries a patient's name, record number, reference or contact details, the case number, the
/// hospital's result or record identifiers, a staff member's reference, or any free text from the hospital system, such
/// as an escalation reason, a referral's rejection reason or an appointment's place. The read is recorded like a
/// worklist read, with the agent version, prompt version and correlation identifier, and what the agent writes is a
/// draft: it is not stored and changes nothing.
/// </para>
/// </remarks>
public static partial class DraftRoleBriefing
{
    /// <summary>The view key the read is recorded under; a refused read appends <c>-denied</c>.</summary>
    public const string AuditView = "role-briefing";

    /// <summary>What the text is for, as the request to the AI provider names it.</summary>
    public const string Purpose = "role_briefing";

    /// <summary>The prompt's version, written into the prompt and returned with the text so a change of wording can be traced.</summary>
    public const string PromptVersion = "role-briefing-v3";

    /// <summary>Most items the agent lists in each section; it counts the rest.</summary>
    public const int MaxItemsPerSection = 5;

    /// <summary>Working days after today that the look-ahead covers.</summary>
    public const int LookaheadDays = 5;

    /// <summary>
    /// The working-day calendar the look-ahead and the working days left use: Monday to Friday, no public holidays. A
    /// placeholder until the team agrees a calendar.
    /// </summary>
    public const string WorkingDayCalendar = "mon-fri-no-holidays-v1";

    /// <summary>Overdue or escalated: act now.</summary>
    public const string ActNow = "P1";

    /// <summary>Due today, or on a non-working day before the next working day.</summary>
    public const string DueToday = "P2";

    /// <summary>Due on one of the look-ahead days.</summary>
    public const string Later = "P3";

    /// <summary>Urgent tier or needing an owner, and not overdue, escalated or due today.</summary>
    public const string Info = "info";

    private static readonly string[] Bands = [ActNow, DueToday, Info, Later];

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
    /// <param name="pathways">The display-step rules and the clock configuration.</param>
    /// <param name="format">Gives local dates, times and facility names.</param>
    /// <param name="agents">Calls the agent on the route <c>Ai:Provider</c> chose.</param>
    /// <param name="options">Whether the agents are on, the agent version and which urgency codes mean urgent.</param>
    /// <param name="clock">The current time, for deadlines and the read audit.</param>
    /// <param name="loggerFactory">Records the reason code when the agent declines.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// 200 with the agent's text; 400, 401 or 403 as for the worklist reads; 409 while the agents are switched off
    /// (<c>Ai:Agents:Enabled</c> is <see langword="false"/> or <c>Ai:Provider</c> is <c>NoOp</c>); 503 when the agent
    /// could not write it.
    /// </returns>
    public static async Task<IResult> Handle([FromQuery] string? facilityId, [FromQuery] string? reason, HttpContext context,
        CareLoopDbContext db, IPathwayProvider pathways, MessageFormat format, IAgentClient agents, IOptions<AgentOptions> options,
        IClock clock, ILoggerFactory loggerFactory, CancellationToken ct)
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
        var input = BuildInput(actor, scopeFacility, outside, cases, pathways, options.Value.UrgentCodes, format, clock.UtcNow);
        var request = new AgentRequest
        {
            Purpose = Purpose, Agent = options.Value.RoleBriefingAgent(), PromptVersion = PromptVersion, Prompt = input.Prompt,
            CorrelationId = Guid.NewGuid(),
        };
        await AgentReplies.AuditAsync(db, actor, clock, AuditView, null, outside, reason, queryText, request, ct);
        return await AgentReplies.AskAsync(agents, request, answer => Read(answer, input), clock,
            loggerFactory.CreateLogger(typeof(DraftRoleBriefing)), ct);
    }

    /// <summary>
    /// Writes the role-briefing prompt: the reader's worklist, banded and ordered, inside <c>&lt;input_json&gt;</c>.
    /// </summary>
    /// <param name="reader">Who asked; only the role is used, and whether a case is theirs.</param>
    /// <param name="facilityId">The facility in scope, or <see langword="null"/> for every facility.</param>
    /// <param name="outsideTeam">Whether the reader is looking at another team's cases.</param>
    /// <param name="cases">The open cases in scope.</param>
    /// <param name="pathways">The display-step rules and the clock configuration.</param>
    /// <param name="urgentCodes">Urgency codes that mean urgent.</param>
    /// <param name="format">Gives local times and facility names.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The prompt, and which case each token stands for. The prompt holds no name, identifier, case number, contact detail, staff reference or free text.</returns>
    internal static BriefingInput BuildInput(Actor reader, string? facilityId, bool outsideTeam, IReadOnlyList<Case> cases,
        IPathwayProvider pathways, IReadOnlyCollection<string> urgentCodes, MessageFormat format, DateTimeOffset now)
    {
        var urgent = new HashSet<string>(urgentCodes, StringComparer.OrdinalIgnoreCase);
        var today = LocalDay(now, format);
        var days = WorkingDaysAfter(today, LookaheadDays);
        var items = cases
            .Select(c => (Case: c, Band: BandOf(c, urgent, days, format, now)))
            .Where(x => x.Band is not null)
            .OrderBy(x => Array.IndexOf(Bands, x.Band)).ThenBy(x => x.Case.DueAt ?? DateTimeOffset.MaxValue).ThenBy(x => x.Case.Id)
            .Select((x, i) => new BriefingItem($"W-{i + 1}", $"P-{i + 1}", x.Band!, x.Case))
            .ToList();
        var inScope = cases.Select(c => c.PathwayId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(pathways.Get).ToList();
        var everyFacility = facilityId is null;
        var input = new
        {
            Role = AgentRole(reader.Role),
            Scope = Scope(reader, facilityId, outsideTeam, format),
            AsOf = AgentInput.Moment(format.InLocalTime(now)),
            CalendarVersion = WorkingDayCalendar,
            ClockConfig = new
            {
                Version = inScope.Count == 0 ? "none" : string.Join(", ", inScope.Select(p => $"{p.Id}@{p.Version}")),
                Status = inScope.Count > 0 && inScope.All(p => p.Deadlines.All(d => d.ClinicallyApproved)) ? "approved" : "provisional",
            },
            Counts = Bands.ToDictionary(band => band, band => items.Count(i => i.Band == band)),
            Items = items.Select(i => Item(i, reader, pathways, urgent, format, now, today, everyFacility)).ToList(),
            Lookahead = days.Select(day => new
            {
                Date = AgentInput.Day(day),
                ItemIds = items.Where(i => i.Band == Later && WorkingDayOnOrBefore(LocalDay(i.Case.DueAt!.Value, format)) == day)
                    .Select(i => i.ItemId).ToList(),
            }).ToList(),
            MaxItemsPerSection,
        };
        return new BriefingInput(AgentInput.Prompt("CareLoop role briefing", PromptVersion, input), items);
    }

    /// <summary>
    /// Reads the agent's briefing and turns it into the words staff read: the headline, then what to act on now and what
    /// is due today, then anything for the reader's role, with each token replaced by its case number.
    /// </summary>
    /// <param name="answer">The agent's answer.</param>
    /// <param name="input">What was sent.</param>
    /// <returns>Up to three short paragraphs, separated by a blank line.</returns>
    /// <exception cref="AgentDeclinedException">The agent declined; or it listed an item or token that was not sent, or
    /// left a P1 item neither listed nor counted.</exception>
    internal static string Read(string answer, BriefingInput input)
    {
        var root = AgentReply.Read(answer);
        var headline = AgentReply.RequiredText(root, "headline");
        if (!root.TryGetProperty("sections", out var sections))
        {
            throw new AgentDeclinedException("MISSING_OUTPUT_FIELD");
        }

        var sent = input.Items.ToDictionary(i => i.ItemId, i => i.Band, StringComparer.Ordinal);
        var intros = new Dictionary<string, string>();
        foreach (var (key, band) in new[] { ("today", ActNow), ("coming_up", DueToday), ("role", Info) })
        {
            var section = Section(sections, key);
            var listed = AgentReply.Texts(section, "item_ids").Distinct(StringComparer.Ordinal).ToList();
            if (listed.Any(id => !sent.ContainsKey(id)))
            {
                throw new AgentDeclinedException("UNKNOWN_ITEM_ID");
            }

            if (listed.Any(id => sent[id] != band))
            {
                throw new AgentDeclinedException("ITEM_IN_WRONG_SECTION");
            }

            if (band == ActNow && listed.Count + Overflow(section) != input.Count(ActNow))
            {
                throw new AgentDeclinedException("P1_NOT_ACCOUNTED_FOR");
            }

            intros[key] = AgentReply.RequiredText(section, "intro");
        }

        var paragraphs = new[]
        {
            headline,
            string.Join(" ", new[] { WithoutNone(intros["today"]), WithoutNone(intros["coming_up"]) }.Where(intro => intro.Length > 0)),
            WithoutNone(intros["role"]),
        };
        return string.Join("\n\n", paragraphs.Where(p => p.Length > 0).Select(p => WithCaseNumbers(p, input)));
    }

    /// <summary>The agent's role name for a CareLoop role.</summary>
    /// <param name="role">The CareLoop role, for example <c>nurse-navigator</c>.</param>
    /// <returns><c>nurse</c>, <c>clinician</c>, <c>programme_coordinator</c>, <c>facility_lead</c> or <c>lab_technician</c>.</returns>
    internal static string AgentRole(string role) => role switch
    {
        DemoRoles.NurseNavigator => "nurse",
        DemoRoles.Coordinator => "programme_coordinator",
        DemoRoles.FacilityLead => "facility_lead",
        DemoRoles.Lab => "lab_technician",
        _ => "clinician",
    };

    /// <summary>
    /// Counts the working days from one local day to another, by <see cref="WorkingDayCalendar"/>.
    /// </summary>
    /// <param name="from">Today.</param>
    /// <param name="to">The deadline's day.</param>
    /// <returns>0 on the same day, the working days after <paramref name="from"/> up to <paramref name="to"/>, or minus those back to it when it is past.</returns>
    internal static int WorkingDaysBetween(DateOnly from, DateOnly to)
    {
        var (start, end, sign) = to >= from ? (from, to, 1) : (to, from, -1);
        var count = 0;
        for (var day = start.AddDays(1); day <= end; day = day.AddDays(1))
        {
            if (IsWorkingDay(day)) count++;
        }

        return sign * count;
    }

    private static object Item(BriefingItem item, Actor reader, IPathwayProvider pathways, HashSet<string> urgent, MessageFormat format,
        DateTimeOffset now, DateOnly today, bool everyFacility)
    {
        var c = item.Case;
        var escalation = c.Escalations.LastOrDefault(e => e.IsOpen);
        return new
        {
            item.ItemId,
            item.Token,
            item.Band,
            Kind = c.NextAction ?? "none",
            Label = AgentWords.NextAction(c.NextAction),
            Stage = AgentWords.Step(CaseDisplayLabels.From(c, pathways.Get(c.PathwayId))),
            ClockId = $"{c.PathwayId}/{c.NextAction ?? "none"}",
            DueAt = c.DueAt is { } due ? AgentInput.Moment(format.InLocalTime(due)) : null,
            WorkingDaysRemaining = c.DueAt is { } deadline ? WorkingDaysBetween(today, LocalDay(deadline, format)) : (int?)null,
            UrgencyTier = c.Urgency is null ? "not_reviewed" : urgent.Contains(c.Urgency) ? "urgent" : "routine",
            OwnerIsUser = IsReaders(c.Owner, reader),
            ActsNext = ActsNext(c.Owner, reader),
            PatientContact = Contact(c),
            NeedsOwner = c.Exceptions.HasFlag(CaseExceptions.Unassigned),
            Escalation = escalation is null
                ? null
                : new { escalation.Level, To = AgentWords.Role(escalation.EscalatedTo.Role), Acknowledged = escalation.IsAcknowledged },
            Facility = everyFacility ? format.Facility(c.FacilityId) : null,
        };
    }

    private static string? BandOf(Case c, HashSet<string> urgent, IReadOnlyList<DateOnly> days, MessageFormat format, DateTimeOffset now)
    {
        if (IsOverdue(c, now) || c.IsEscalated) return ActNow;
        var workingDay = c.DueAt is { } due ? WorkingDayOnOrBefore(LocalDay(due, format)) : (DateOnly?)null;
        if (workingDay <= LocalDay(now, format)) return DueToday;
        if (c.Urgency is not null && urgent.Contains(c.Urgency) || c.Exceptions.HasFlag(CaseExceptions.Unassigned)) return Info;
        return workingDay is { } day && days.Contains(day) ? Later : null;
    }

    // A deadline on a non-working day counts on the working day before it, so it is never seen for the first time late:
    // on a Friday, a deadline on Saturday is due today; on a Monday, it is listed under Friday.
    private static DateOnly WorkingDayOnOrBefore(DateOnly day)
    {
        while (!IsWorkingDay(day))
        {
            day = day.AddDays(-1);
        }

        return day;
    }

    private static List<DateOnly> WorkingDaysAfter(DateOnly today, int count)
    {
        var days = new List<DateOnly>();
        for (var day = today.AddDays(1); days.Count < count; day = day.AddDays(1))
        {
            if (IsWorkingDay(day)) days.Add(day);
        }

        return days;
    }

    private static bool IsWorkingDay(DateOnly day) => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    private static DateOnly LocalDay(DateTimeOffset at, MessageFormat format) => DateOnly.FromDateTime(format.InLocalTime(at).DateTime);

    private static JsonElement Section(JsonElement sections, string key)
    {
        if (sections.ValueKind == JsonValueKind.Object && sections.TryGetProperty(key, out var named) && named.ValueKind == JsonValueKind.Object)
        {
            return named;
        }

        if (sections.ValueKind == JsonValueKind.Array)
        {
            foreach (var section in sections.EnumerateArray())
            {
                var name = AgentReply.Text(section, "name") ?? AgentReply.Text(section, "section") ?? AgentReply.Text(section, "id");
                if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return section;
                }
            }
        }

        throw new AgentDeclinedException("MISSING_OUTPUT_FIELD");
    }

    private static int Overflow(JsonElement section) =>
        section.TryGetProperty("overflow_count", out var value) switch
        {
            false => 0,
            true when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= 0 => number,
            true when value.ValueKind == JsonValueKind.Null => 0,
            _ => throw new AgentDeclinedException("MALFORMED_OUTPUT"),
        };

    // An empty band's intro is "None."; the coordinator's may go on, as in "None. Deadlines are provisional.".
    private static string WithoutNone(string intro) =>
        intro.StartsWith("None", StringComparison.OrdinalIgnoreCase) && intro[4..].TrimStart() is var rest && (rest.Length == 0 || rest[0] == '.')
            ? rest.TrimStart('.').Trim()
            : intro;

    // Puts the case number, as the dashboard shows it, where the agent wrote a token or an item ID.
    private static string WithCaseNumbers(string paragraph, BriefingInput input)
    {
        var byName = input.Items.SelectMany(i => new[] { (i.Token, i.Case), (i.ItemId, i.Case) }).ToDictionary(p => p.Item1, p => p.Item2, StringComparer.Ordinal);
        return Token().Replace(paragraph, match =>
        {
            if (!byName.TryGetValue(match.Value, out var @case))
            {
                throw new AgentDeclinedException("UNKNOWN_TOKEN");
            }

            var before = paragraph[..match.Index].TrimEnd();
            var startsSentence = before.Length == 0 || before.EndsWith('.') || before.EndsWith('?') || before.EndsWith('!');
            return $"{(startsSentence ? "Case" : "case")} {NotificationRouter.CaseReferenceFor(@case.Id)}";
        });
    }

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

    private static string Scope(Actor reader, string? facilityId, bool outsideTeam, MessageFormat format)
    {
        var place = facilityId is null ? "every facility" : format.Facility(facilityId);
        if (reader.Role == DemoRoles.Coordinator)
        {
            return $"{place}; only the cases escalated to the programme coordinator";
        }

        return outsideTeam ? $"{place}, another team's cases" : $"{place}, the reader's team";
    }

    private static bool IsReaders(CaseOwner owner, Actor reader) =>
        !owner.IsQueue && string.Equals(WithoutType(owner.Reference), WithoutType(reader.Reference), StringComparison.OrdinalIgnoreCase);

    private static string ActsNext(CaseOwner owner, Actor reader)
    {
        if (IsReaders(owner, reader))
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

    [GeneratedRegex(@"\b[PW]-\d+\b")]
    private static partial Regex Token();

    /// <summary>One case in the briefing: the item ID and token made up for this prompt, and its band.</summary>
    /// <param name="ItemId">For example <c>W-1</c>.</param>
    /// <param name="Token">For example <c>P-1</c>; the only way the prompt names the case.</param>
    /// <param name="Band"><c>P1</c>, <c>P2</c>, <c>info</c> or <c>P3</c>.</param>
    /// <param name="Case">The case, kept in the API to put its case number back.</param>
    internal sealed record BriefingItem(string ItemId, string Token, string Band, Case Case);

    /// <summary>A briefing prompt and the cases its tokens stand for.</summary>
    /// <param name="Prompt">The prompt.</param>
    /// <param name="Items">The items, in the order sent.</param>
    internal sealed record BriefingInput(string Prompt, IReadOnlyList<BriefingItem> Items)
    {
        /// <summary>How many items were sent in a band.</summary>
        /// <param name="band">The band.</param>
        /// <returns>The count.</returns>
        public int Count(string band) => Items.Count(i => i.Band == band);
    }
}
