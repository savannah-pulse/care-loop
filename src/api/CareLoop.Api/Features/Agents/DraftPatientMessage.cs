using System.Text;
using System.Text.Json;
using CareLoop.Api.Features.Common;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Ai;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Features.Agents;

/// <summary>
/// Slice for <c>POST /cases/{caseId}/patient-message-draft</c>: asks the patient-notification agent to draft a text
/// message or email to the patient, as a preview for a nurse navigator or clinician. Nothing is sent.
/// </summary>
/// <remarks>
/// <para>
/// Patient messages go out from approved templates (<c>Infrastructure/Templates</c>), never from a model. This preview
/// shows staff how the agent would word a category A, administrative message; it is not stored, is never sent and
/// changes nothing on the case.
/// </para>
/// <para>
/// The prompt never carries the case's next action. Several next actions reveal clinical state ("review the result",
/// "start the treatment", "send a new referral"), and the reporting physician gives results (DoH §3.8.1), so an
/// instruction not to hint at a result cannot help once the input itself is the hint. Instead each next action is
/// mapped here, on the API side, to one patient-safe step (<see cref="PatientStepFor"/>): the care team would like to
/// talk with you, please confirm you got our message, we will call you back, please contact us, or we would like to
/// find a new time for your appointment. A next action this map does not know becomes the first, the most neutral.
/// </para>
/// <para>
/// The agent (AG2 in the Foundry project) fills in one template inside <c>&lt;input_json&gt;</c>: CareLoop builds a
/// category A template, without result content, from locked spans it copies word for word, slots it fills with facts
/// and two adaptable spans (the greeting and the closing). The facts are the facility's name, the patient-safe step, an
/// upcoming appointment's day and time in local time, and <c>{first name}</c> in place of the name; the two reply codes
/// (1 means "I got this message", 2 asks for a call) and <c>{place}</c> in place of the appointment's place are locked
/// text. The answer is accepted only when every locked span is word for word, every slot is exactly its fact and no
/// number appears in an adaptable span; otherwise, or on a refusal, it is the calm 503, with the reason code in the log.
/// It carries no name, contact detail, record number, patient reference, result, and no free text from the hospital
/// system. Access follows the case reads, and the read is recorded like one, with the agent version, prompt version and
/// correlation identifier.
/// </para>
/// </remarks>
public static class DraftPatientMessage
{
    /// <summary>The view key the read is recorded under; a refused read appends <c>-denied</c>.</summary>
    public const string AuditView = "patient-message-draft";

    /// <summary>What the text is for, as the request to the AI provider names it.</summary>
    public const string Purpose = "patient_message_preview";

    /// <summary>The prompt's version, written into the prompt and returned with the text so a change of wording can be traced.</summary>
    public const string PromptVersion = "patient-message-v3";

    /// <summary>The placeholder the draft uses where the patient's first name would go.</summary>
    public const string FirstNamePlaceholder = "{first name}";

    /// <summary>The placeholder the draft uses where the appointment's place would go: the place is free text from the hospital system, so it is never sent.</summary>
    public const string PlacePlaceholder = "{place}";

    /// <summary>The two reply codes every patient message ends with, as the approved templates word them.</summary>
    public const string ReplyCodes = "Reply 1 to confirm you got this message. Reply 2 if you would like your care team to call you.";

    /// <summary>The email preview's subject line.</summary>
    public const string EmailSubject = "Subject: A message from your care team";

    /// <summary>Most characters in a text message preview: three SMS segments of 153 characters. A placeholder until the team sets a limit.</summary>
    public const int MaxSmsCharacters = 459;

    /// <summary>Most characters in an email preview.</summary>
    public const int MaxEmailCharacters = 1000;

    /// <summary>The roles that may preview a message to a patient.</summary>
    /// <remarks>Proposed, pending the team's decision (the design proposes the coordinator or a physician); see <c>docs/workstreams.md</c>.</remarks>
    public static readonly IReadOnlyList<string> AllowedRoles = [DemoRoles.NurseNavigator, DemoRoles.Clinician];

    /// <summary>
    /// The only steps a patient message may name: category A, administrative wording with no clinical result in it.
    /// </summary>
    public static readonly IReadOnlyList<string> PatientSafeSteps =
    [
        TalkWithYou,
        "Your care team sent you a message and would like to know that you got it.",
        "Your care team will call you back, as you asked.",
        "Please contact your care team.",
        "Your care team would like to find a new time for your appointment.",
    ];

    // The most neutral step, for every next action that would reveal clinical state and for any key not listed below.
    private const string TalkWithYou = "Your care team would like to talk with you.";

    private static readonly IReadOnlyDictionary<string, string> PatientSteps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["acknowledge-notification"] = PatientSafeSteps[1],
        ["call-patient"] = PatientSafeSteps[2],
        ["contact-patient-another-way"] = PatientSafeSteps[3],
        ["rebook-appointment"] = PatientSafeSteps[4],
    };

    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapDraftPatientMessage(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/cases/{caseId:guid}/patient-message-draft", Handle).WithName("DraftPatientMessage").WithTags("Ai")
            .Produces<AiTextResponse>().Produces(StatusCodes.Status404NotFound).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return routes;
    }

    /// <summary>Asks the patient-notification agent for a preview of a message to the patient. Nothing is sent.</summary>
    /// <param name="caseId">The case.</param>
    /// <param name="channel">The channel to draft for, <c>Sms</c> (the default) or <c>Email</c>.</param>
    /// <param name="reason">Access reason required outside the reader's facility, recorded with the read.</param>
    /// <param name="context">The caller-stated staff identity.</param>
    /// <param name="db">The read context.</param>
    /// <param name="format">Gives local dates, times and facility names.</param>
    /// <param name="agents">Calls the agent on the route <c>Ai:Provider</c> chose.</param>
    /// <param name="options">Whether the agents are on, and the agent version.</param>
    /// <param name="ai">The shared AI settings: <c>Ai:TimeoutSeconds</c> bounds every attempt together.</param>
    /// <param name="clock">The current time and the read audit clock.</param>
    /// <param name="loggerFactory">Records the reason code when the agent declines or its answer does not match the template.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// 200 with the draft; 400 for a malformed request; 401 without identity; 403 for a role other than nurse navigator
    /// or clinician, or a case the reader may not open; 404 for an unknown case; 409 while the agents are switched off
    /// (<c>Ai:Agents:Enabled</c> is <see langword="false"/> or <c>Ai:Provider</c> is <c>NoOp</c>); 503 when the agent
    /// could not write it.
    /// </returns>
    public static async Task<IResult> Handle(Guid caseId, HttpContext context, CareLoopDbContext db, MessageFormat format,
        IAgentClient agents, IOptions<AgentOptions> options, IOptions<AiOptions> ai, IClock clock, ILoggerFactory loggerFactory, CancellationToken ct,
        [FromQuery] NotificationChannel channel = NotificationChannel.Sms, [FromQuery] string? reason = null)
    {
        if (ReadAccess.Identity(context, out var actor) is { } identityError) return identityError;
        if (ReadAccess.ValidateReason(reason) is { } reasonError) return reasonError;
        if (!Enum.IsDefined(channel))
            return Results.Problem("The channel must be Sms or Email.", statusCode: 400);
        if (!AllowedRoles.Contains(actor.Role))
            return Results.Problem("Only a nurse navigator or a clinician may preview a message to the patient.", statusCode: 403);
        if (!AgentReplies.IsOn(options.Value, agents)) return AgentReplies.SwitchedOff("PatientMessageDraft", options.Value);

        var @case = await db.Cases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == caseId, ct);
        var outside = @case is not null && @case.FacilityId != actor.FacilityId;
        var refusal = @case is null ? null : ReadAccess.CasePermission(@case, actor, reason);
        var query = ReadAccess.Query(("channel", channel));
        if (refusal is not null || @case is null)
        {
            await ReadAccess.AuditAsync(db, actor, clock, refusal is null ? AuditView : AuditView + "-denied", caseId, outside, reason, query, ct);
            return refusal ?? Results.NotFound();
        }

        var placeNamed = await AppointmentPlaceNamedAsync(db, @case, ct);
        var input = BuildInput(@case, placeNamed, channel, format, clock.UtcNow);
        var request = new AgentRequest
        {
            Purpose = Purpose, Agent = options.Value.PatientNotificationAgent(), PromptVersion = PromptVersion, Prompt = input.Prompt,
            CorrelationId = Guid.NewGuid(),
        };
        return await AgentReplies.AskAsync(agents, request, answer => Read(answer, input),
            attempt => AgentReplies.AuditAsync(db, actor, clock, AuditView, caseId, outside, reason, query, attempt, ct),
            AgentReplies.Budget(ai.Value), clock, loggerFactory.CreateLogger(typeof(DraftPatientMessage)), ct);
    }

    /// <summary>
    /// Maps a case's next action to the one patient-safe step a patient message may name.
    /// </summary>
    /// <param name="nextAction">The case's next-action key, or <see langword="null"/>.</param>
    /// <returns>
    /// One of <see cref="PatientSafeSteps"/>: please confirm you got our message (<c>acknowledge-notification</c>), we will
    /// call you back (<c>call-patient</c>), please contact your care team (<c>contact-patient-another-way</c>), a new time
    /// for your appointment (<c>rebook-appointment</c>), and for every other key, including those that would reveal a
    /// result, a review, a referral, a treatment or a closure, "your care team would like to talk with you".
    /// </returns>
    public static string PatientStepFor(string? nextAction) =>
        nextAction is not null && PatientSteps.TryGetValue(nextAction.Trim(), out var step) ? step : TalkWithYou;

    /// <summary>
    /// Writes the patient-message prompt: a category A template, its facts, the channel and the character limit, inside
    /// <c>&lt;input_json&gt;</c>.
    /// </summary>
    /// <param name="case">The case; only its next action (as a patient-safe step), appointment time and facility are used.</param>
    /// <param name="placeNamed">Whether the hospital system named the booked appointment's place. The place itself is never sent.</param>
    /// <param name="channel">The channel to draft for.</param>
    /// <param name="format">Gives local dates, times and facility names.</param>
    /// <param name="now">The current time; only an upcoming appointment is described.</param>
    /// <returns>The prompt and the template it carries. It holds no name, contact detail, identifier, result, next action or free text.</returns>
    internal static MessageInput BuildInput(Case @case, bool placeNamed, NotificationChannel channel, MessageFormat format, DateTimeOffset now)
    {
        var sms = channel == NotificationChannel.Sms;
        var recorded = AgentInput.Moment(format.InLocalTime(now));
        var facts = new List<MessageFact>
        {
            new("F1", "first_name", FirstNamePlaceholder),
            new("F2", "care_team", format.Facility(@case.FacilityId)),
            new("F3", "next_step", PatientStepFor(@case.NextAction)),
        };
        var spans = new List<MessageSpan>();
        if (!sms)
        {
            spans.Add(MessageSpan.Locked("subject", EmailSubject));
        }

        spans.AddRange(
        [
            MessageSpan.Adaptable("greeting", "Hello"),
            MessageSpan.Slot("first_name", "first_name"),
            MessageSpan.Locked("greeting_end", ","),
            MessageSpan.Locked("care_team_intro", "this is your care team at"),
            MessageSpan.Slot("care_team", "care_team"),
            MessageSpan.Locked("care_team_end", "."),
            MessageSpan.Slot("next_step", "next_step"),
        ]);
        if (@case.Appointment is { Status: AppointmentStatus.Scheduled, ScheduledFor: { } at } && at >= now)
        {
            facts.Add(new MessageFact("F4", "appointment_time", $"{format.Date(at)} at {format.Time(at)}"));
            spans.Add(MessageSpan.Locked("appointment_intro", "Your appointment is on"));
            spans.Add(MessageSpan.Slot("appointment_time", "appointment_time"));
            spans.Add(MessageSpan.Locked("appointment_place", placeNamed ? $". Place: {PlacePlaceholder}." : ". Place: to be confirmed."));
        }

        spans.Add(MessageSpan.Locked("reply_codes", ReplyCodes));
        spans.Add(MessageSpan.Adaptable("closing", "Thank you."));
        var template = new MessageTemplate(sms ? "careloop-preview-a.sms" : "careloop-preview-a", "1", "A", "en", spans);
        var maxChars = sms ? MaxSmsCharacters : MaxEmailCharacters;
        var input = new
        {
            Stage = "patient_contact",
            Language = template.Language,
            Channel = sms ? "sms" : "email",
            Template = new
            {
                template.Id, template.Version, Class = template.Class, template.Language,
                Spans = spans.Select(s => new { s.SpanId, s.Kind, s.Text, s.SlotName, MaxChars = (int?)null }).ToList(),
            },
            Facts = facts.Select(f => new { f.FactId, f.FactType, f.Value, RecordedAt = recorded, ReviewStatus = "final" }).ToList(),
            Adaptation = new { ReadingLevel = "plain", Tone = "warm-neutral", GreetingStyle = "first name" },
            MaxChars = maxChars,
        };
        return new MessageInput(AgentInput.Prompt("CareLoop patient message draft", PromptVersion, input), template, facts, maxChars);
    }

    /// <summary>
    /// Reads the agent's filled-in template and checks it against what was sent: the same spans in the same order,
    /// every locked span word for word, every slot holding its fact, and no number added to an adaptable span.
    /// </summary>
    /// <param name="answer">The agent's answer.</param>
    /// <param name="input">What was sent.</param>
    /// <returns>The message as staff read it; for email, the subject on its own line first.</returns>
    /// <exception cref="AgentDeclinedException">The agent declined, or its spans do not match the template
    /// (<c>SPANS_CHANGED</c>, <c>LOCKED_SPAN_CHANGED</c>, <c>SLOT_VALUE_CHANGED</c>, <c>ADAPTED_SPAN_ADDED_NUMBER</c>), or
    /// the message is over the channel's limit (<c>TOO_LONG</c>).</exception>
    internal static string Read(string answer, MessageInput input)
    {
        var root = AgentReply.Read(answer);
        var sent = input.Template.Spans;
        var written = WrittenSpans(root, sent);
        if (written.Count != sent.Count)
        {
            throw new AgentDeclinedException("SPANS_CHANGED");
        }

        var texts = new List<string>();
        for (var i = 0; i < sent.Count; i++)
        {
            var span = sent[i];
            if (written[i].SpanId != span.SpanId)
            {
                throw new AgentDeclinedException("SPANS_CHANGED");
            }

            var text = written[i].Text;
            switch (span.Kind)
            {
                case "locked" when text != span.Text:
                    throw new AgentDeclinedException("LOCKED_SPAN_CHANGED");
                case "slot" when text != input.Facts.Single(f => f.FactType == span.SlotName).Value:
                    throw new AgentDeclinedException("SLOT_VALUE_CHANGED");
                case "adaptable" when text.Any(char.IsDigit) && !span.Text!.Any(char.IsDigit):
                    throw new AgentDeclinedException("ADAPTED_SPAN_ADDED_NUMBER");
            }

            texts.Add(text);
        }

        var subject = input.Template.Spans[0].SpanId == "subject" ? texts[0] : null;
        var body = new StringBuilder();
        // An adaptable span may come back empty: the agent shortens those first to fit the channel's limit.
        foreach (var text in (subject is null ? texts : texts.Skip(1)).Where(text => text.Length > 0))
        {
            if (body.Length > 0 && !".,;:!?".Contains(text[0], StringComparison.Ordinal))
            {
                body.Append(' ');
            }

            body.Append(text);
        }

        var message = subject is null ? body.ToString() : $"{subject}\n\n{body}";
        return message.Length > input.MaxChars ? throw new AgentDeclinedException("TOO_LONG") : message;
    }
    // The agent's spans: an array of {span_id, text} objects, under "spans" or another name, or one object keyed by span
    // ID. The shape varies while the agent version has no response schema (see docs/ai-design/integration.md); what the
    // spans say is checked the same way whatever the shape.
    private static List<(string SpanId, string Text)> WrittenSpans(JsonElement root, IReadOnlyList<MessageSpan> sent)
    {
        var array = root.TryGetProperty("spans", out var spans) && spans.ValueKind == JsonValueKind.Array
            ? spans
            : root.EnumerateObject().Select(p => p.Value).FirstOrDefault(v => v.ValueKind == JsonValueKind.Array && v.GetArrayLength() > 0
                && v.EnumerateArray().All(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("span_id", out _)));
        if (array.ValueKind == JsonValueKind.Array)
        {
            return array.EnumerateArray()
                .Select(e => (AgentReply.Text(e, "span_id") ?? throw new AgentDeclinedException("MALFORMED_OUTPUT"),
                    e.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
                        ? text.GetString()!.Trim()
                        : throw new AgentDeclinedException("MALFORMED_OUTPUT")))
                .ToList();
        }

        return sent.All(s => root.TryGetProperty(s.SpanId, out var value) && value.ValueKind == JsonValueKind.String)
            ? sent.Select(s => (s.SpanId, root.GetProperty(s.SpanId).GetString()!.Trim())).ToList()
            : throw new AgentDeclinedException("MISSING_OUTPUT_FIELD");
    }

    /// <summary>
    /// Tells whether the hospital system named a place for the booked appointment, from its latest applied booking.
    /// </summary>
    /// <param name="db">The read context.</param>
    /// <param name="case">The case.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns><see langword="true"/> when the latest booking named a place; the place itself is not read out.</returns>
    /// <remarks>The place is not kept on the case; it travels in the stored appointment event, which is where the patient's own appointment message takes it from. It is free text, so the prompt only learns whether there is one.</remarks>
    private static async Task<bool> AppointmentPlaceNamedAsync(CareLoopDbContext db, Case @case, CancellationToken ct)
    {
        if (@case.Appointment is not { Status: AppointmentStatus.Scheduled })
        {
            return false;
        }

        var bookings = await db.SourceEvents.AsNoTracking()
            .Where(e => e.CaseId == @case.Id && e.Type == CaseEventType.AppointmentUpdated && e.Disposition == SourceEventDisposition.Applied)
            .Select(e => new { e.ReceivedAt, e.PayloadJson })
            .ToListAsync(ct);
        foreach (var booking in bookings.OrderByDescending(b => b.ReceivedAt))
        {
            using var payload = JsonDocument.Parse(booking.PayloadJson);
            var root = payload.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String
                && status.GetString() == nameof(AppointmentStatus.Scheduled))
            {
                return root.TryGetProperty("location", out var location) && location.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(location.GetString());
            }
        }

        return false;
    }

    /// <summary>One span of the template sent to the agent.</summary>
    /// <param name="SpanId">The span's ID.</param>
    /// <param name="Kind"><c>locked</c> (copied word for word), <c>adaptable</c> (tone and reading level only) or <c>slot</c> (one fact).</param>
    /// <param name="Text">The text of a locked or adaptable span.</param>
    /// <param name="SlotName">The fact type a slot takes.</param>
    internal sealed record MessageSpan(string SpanId, string Kind, string? Text, string? SlotName)
    {
        public static MessageSpan Locked(string id, string text) => new(id, "locked", text, null);

        public static MessageSpan Adaptable(string id, string text) => new(id, "adaptable", text, null);

        public static MessageSpan Slot(string id, string slotName) => new(id, "slot", null, slotName);
    }

    /// <summary>One fact a slot may take; every fact is final, and none is a result.</summary>
    /// <param name="FactId">For example <c>F1</c>.</param>
    /// <param name="FactType">The slot it fills, for example <c>next_step</c>.</param>
    /// <param name="Value">The words, exactly as the slot must hold them.</param>
    internal sealed record MessageFact(string FactId, string FactType, string Value);

    /// <summary>The category A template the preview is drafted from.</summary>
    /// <param name="Id">The template's ID.</param>
    /// <param name="Version">Its version.</param>
    /// <param name="Class"><c>A</c>: logistics, no result content.</param>
    /// <param name="Language">The template's language.</param>
    /// <param name="Spans">Its spans, in order.</param>
    internal sealed record MessageTemplate(string Id, string Version, string Class, string Language, IReadOnlyList<MessageSpan> Spans);

    /// <summary>A patient-message prompt and what it carries, to check the answer against.</summary>
    /// <param name="Prompt">The prompt.</param>
    /// <param name="Template">The template sent.</param>
    /// <param name="Facts">The facts sent.</param>
    /// <param name="MaxChars">The channel's limit.</param>
    internal sealed record MessageInput(string Prompt, MessageTemplate Template, IReadOnlyList<MessageFact> Facts, int MaxChars);
}