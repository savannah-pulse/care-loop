using CareLoop.Api.Features.Common;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Ai;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Ai;

/// <summary>
/// <c>GET /cases/{caseId}/patient-message-draft</c>: an AI draft of a message to the patient, for a nurse navigator or
/// clinician to read. Nothing is sent and nothing is stored.
/// </summary>
/// <remarks>
/// <para>
/// The draft is one approved administrative template with its placeholders filled in. The API chooses the template from
/// what is recorded on the case (<see cref="TemplateFor"/>) and supplies the values; the model changes no other wording.
/// The templates' wording is in <c>Prompts/patient-message.md</c>.
/// </para>
/// <para>
/// The model is never given the patient's name, contact details, identifiers, the result, the appointment's place or
/// the case's next action. The name and the clinic's contact number go as the placeholders
/// <see cref="FirstNamePlaceholder"/> and <see cref="ClinicContactPlaceholder"/>, which the reviewer fills in. Only
/// templates with no result content can be chosen: the reporting physician gives results, so the templates that invite
/// a patient to discuss a result need a clinician's recorded authorization, which CareLoop does not hold.
/// </para>
/// </remarks>
public static class PatientMessageDraft
{
    /// <summary>What stands in the draft for the patient's first name; the name itself is never sent.</summary>
    public const string FirstNamePlaceholder = "{first name}";

    /// <summary>What stands in the draft for the clinic's contact number; CareLoop holds none, so the reviewer fills it in.</summary>
    public const string ClinicContactPlaceholder = "{clinic contact}";

    /// <summary>The roles that may ask for a draft.</summary>
    public static readonly IReadOnlyList<string> AllowedRoles = [DemoRoles.NurseNavigator, DemoRoles.Clinician];

    /// <summary>Maps the route.</summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapPatientMessageDraft(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/cases/{caseId:guid}/patient-message-draft", Handle).WithName("GetPatientMessageDraft").WithTags("Ai")
            .Produces<AiTextResponse>().Produces(StatusCodes.Status404NotFound).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return routes;
    }

    internal static async Task<IResult> Handle(Guid caseId, HttpContext context, CareLoopDbContext db, MessageFormat format, AiText ai,
        IClock clock, CancellationToken ct, [FromQuery] NotificationChannel channel = NotificationChannel.Sms, [FromQuery] string? reason = null)
    {
        if (ReadAccess.Identity(context, out var actor) is { } error) return error;
        if (ReadAccess.ValidateReason(reason) is { } invalid) return invalid;
        if (!Enum.IsDefined(channel)) return Results.Problem("The channel must be Sms or Email.", statusCode: 400);
        if (!AllowedRoles.Contains(actor.Role))
            return Results.Problem("Only a nurse navigator or a clinician may ask for a draft message to the patient.", statusCode: 403);
        if (!ai.IsOn) return AiText.SwitchedOff();

        var @case = await db.Cases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == caseId, ct);
        var refusal = @case is null ? null : ReadAccess.CasePermission(@case, actor, reason);
        var outside = @case is not null && @case.FacilityId != actor.FacilityId;
        await ReadAccess.AuditAsync(db, actor, clock, refusal is null ? "patient-message-draft" : "patient-message-draft-denied", caseId,
            outside, reason, ReadAccess.Query(("channel", channel)), ct);
        if (@case is null) return Results.NotFound();
        if (refusal is not null) return refusal;

        return await ai.WriteAsync(AiPrompts.PatientMessage, BuildData(@case, channel, format, clock.UtcNow), refresh: false, reuse: false, ct);
    }

    /// <summary>Chooses the approved template that fits what is recorded on the case.</summary>
    /// <param name="case">The case.</param>
    /// <param name="now">The time now.</param>
    /// <returns>
    /// <c>T02</c> (appointment reminder) when an appointment is booked and still to come; <c>T03</c> (missed appointment
    /// follow-up) when the last appointment was missed; <c>T06</c> (unsuccessful contact follow-up) when the patient could
    /// not be reached; otherwise <c>T05</c> (the care team asks the patient to get in touch).
    /// </returns>
    /// <remarks>Chosen in code from recorded state, never by the model, and never from the case's next action, which can reveal clinical state.</remarks>
    public static string TemplateFor(Case @case, DateTimeOffset now) => @case switch
    {
        { Appointment: { Status: AppointmentStatus.Scheduled, ScheduledFor: { } at } } when at >= now => "T02",
        { Appointment: { Status: AppointmentStatus.Missed, ScheduledFor: not null } } => "T03",
        _ when @case.Exceptions.HasFlag(CaseExceptions.Unreachable) => "T06",
        _ => "T05",
    };

    /// <summary>Builds what the model is given for one case: the template to fill and the values to fill it with.</summary>
    /// <param name="case">The case.</param>
    /// <param name="channel">Text message or email.</param>
    /// <param name="format">Local dates, times and the clinic's name.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The template's identifier and its values; nothing that identifies the patient.</returns>
    internal static object BuildData(Case @case, NotificationChannel channel, MessageFormat format, DateTimeOffset now)
    {
        var template = TemplateFor(@case, now);
        var appointment = template is "T02" or "T03" ? @case.Appointment!.ScheduledFor : null;
        return new
        {
            Channel = channel == NotificationChannel.Sms ? "text message" : "email",
            Template = template,
            Values = new
            {
                PatientName = FirstNamePlaceholder,
                FacilityName = format.Facility(@case.FacilityId),
                FacilityContact = ClinicContactPlaceholder,
                AppointmentDate = appointment is { } day ? format.Date(day) : null,
                AppointmentTime = template == "T02" && appointment is { } time ? format.Time(time) : null,
            },
        };
    }
}
