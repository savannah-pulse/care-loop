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
/// shows staff how the agent would put the case's next step into words; it is not stored, is never sent and changes
/// nothing on the case.
/// </para>
/// <para>
/// The prompt carries the case's next action, the booked appointment's day, time and place in local time when one is
/// upcoming, the channel and the two reply codes (1 means "I got this message", 2 asks for a call). It carries no name,
/// contact detail, record number, patient reference or result: the agent writes <c>{first name}</c> where the name
/// would go, and is told never to mention or hint at a result, because the reporting physician gives results. Access
/// follows the case reads, and the read is recorded like one, with the agent version, prompt version and correlation
/// identifier.
/// </para>
/// </remarks>
public static class DraftPatientMessage
{
    /// <summary>The view key the read is recorded under; a refused read appends <c>-denied</c>.</summary>
    public const string AuditView = "patient-message-draft";

    /// <summary>What the text is for, as the request to the AI provider names it.</summary>
    public const string Purpose = "patient_message_preview";

    /// <summary>The prompt's version, written into the prompt and returned with the text so a change of wording can be traced.</summary>
    public const string PromptVersion = "patient-message-v1";

    /// <summary>The placeholder the draft uses where the patient's first name would go.</summary>
    public const string FirstNamePlaceholder = "{first name}";

    /// <summary>The roles that may preview a message to a patient.</summary>
    public static readonly IReadOnlyList<string> AllowedRoles = [DemoRoles.NurseNavigator, DemoRoles.Clinician];

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
    /// <param name="clock">The current time and the read audit clock.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// 200 with the draft; 400 for a malformed request; 401 without identity; 403 for a role other than nurse navigator
    /// or clinician, or a case the reader may not open; 404 for an unknown case; 409 while the agents are switched off
    /// (<c>Ai:Agents:Enabled</c> is <see langword="false"/> or <c>Ai:Provider</c> is <c>NoOp</c>); 503 when the agent
    /// could not write it.
    /// </returns>
    public static async Task<IResult> Handle(Guid caseId, HttpContext context, CareLoopDbContext db, MessageFormat format,
        IAgentClient agents, IOptions<AgentOptions> options, IClock clock, CancellationToken ct,
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

        var place = await AppointmentPlaceAsync(db, @case, ct);
        var request = new AgentRequest
        {
            Purpose = Purpose, Agent = options.Value.PatientNotificationAgent(), PromptVersion = PromptVersion,
            Prompt = BuildPrompt(@case, place, channel, format, clock.UtcNow), CorrelationId = Guid.NewGuid(),
        };
        await AgentReplies.AuditAsync(db, actor, clock, AuditView, caseId, outside, reason, query, request, ct);
        return await AgentReplies.AskAsync(agents, request, clock, ct);
    }

    /// <summary>
    /// Writes the patient-message prompt.
    /// </summary>
    /// <param name="case">The case; only its next action, appointment time and facility are used.</param>
    /// <param name="place">Where the appointment is, as the hospital system named it, or <see langword="null"/>.</param>
    /// <param name="channel">The channel to draft for.</param>
    /// <param name="format">Gives local dates, times and facility names.</param>
    /// <param name="now">The current time; only an upcoming appointment is described.</param>
    /// <returns>The plain-text prompt. It holds no name, contact detail, identifier or result.</returns>
    internal static string BuildPrompt(Case @case, string? place, NotificationChannel channel, MessageFormat format, DateTimeOffset now)
    {
        var sms = channel == NotificationChannel.Sms;
        var prompt = new StringBuilder();
        prompt.AppendLine($"CareLoop patient message draft, prompt version {PromptVersion}.");
        prompt.AppendLine(sms
            ? "Draft one text message from the care team to a patient, under 320 characters."
            : "Draft one short email from the care team to a patient: a subject line, then the body.");
        prompt.AppendLine($"It is a preview for staff and will not be sent. Write {FirstNamePlaceholder} where the patient's first name goes: you are not given any name. Use plain, warm words and British spelling.");
        prompt.AppendLine("Never mention or hint at a screening result, a diagnosis or how urgent anything is: the patient's doctor gives results. Add no phone number, link or address that is not below.");
        prompt.AppendLine("End with the two reply codes exactly as written below.");
        prompt.AppendLine();
        prompt.AppendLine($"Channel: {(sms ? "text message (SMS)" : "email")}.");
        prompt.AppendLine($"Care team: {format.Facility(@case.FacilityId)}.");
        prompt.AppendLine($"The care team's next step: {AgentWords.NextAction(@case.NextAction)}.");
        prompt.AppendLine(@case.Appointment is { Status: AppointmentStatus.Scheduled, ScheduledFor: { } at } && at >= now
            ? $"Appointment: {format.Date(at)} at {format.Time(at)}, local time. Place: {(string.IsNullOrWhiteSpace(place) ? "to be confirmed" : place.Trim())}."
            : "Appointment: none booked yet.");
        prompt.AppendLine("Reply codes: \"Reply 1 to confirm you got this message.\" and \"Reply 2 if you would like your care team to call you.\"");
        return prompt.ToString();
    }

    /// <summary>
    /// Finds where the booked appointment is, from the hospital system's latest applied booking.
    /// </summary>
    /// <param name="db">The read context.</param>
    /// <param name="case">The case.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The place as the hospital system named it, or <see langword="null"/> when there is no booking or it named none.</returns>
    /// <remarks>The place is not kept on the case; it travels in the stored appointment event, which is where the patient's own appointment message takes it from.</remarks>
    private static async Task<string?> AppointmentPlaceAsync(CareLoopDbContext db, Case @case, CancellationToken ct)
    {
        if (@case.Appointment is not { Status: AppointmentStatus.Scheduled })
        {
            return null;
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
                    ? location.GetString()
                    : null;
            }
        }

        return null;
    }
}
