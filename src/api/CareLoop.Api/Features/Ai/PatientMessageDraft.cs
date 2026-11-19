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
/// The model is told only what the message is for, in a sentence that reveals no result (<see cref="PurposeFor"/>), the
/// clinic's name, the channel and its length limit, and an upcoming appointment's day and time. It is never given the
/// patient's name, contact details, identifiers, the result, the appointment's place or the case's next action: the
/// reporting physician gives results, so a next action such as "review the result" must not reach the prompt.
/// </para>
/// <para>The wording rules for the message are in <c>Prompts/patient-message.md</c>.</para>
/// </remarks>
public static class PatientMessageDraft
{
    /// <summary>What a message is for when the next action could reveal clinical state, and for any next action not listed.</summary>
    public const string NeutralPurpose = "The care team would like to talk with the patient.";

    /// <summary>The roles that may ask for a draft.</summary>
    public static readonly IReadOnlyList<string> AllowedRoles = [DemoRoles.NurseNavigator, DemoRoles.Clinician];

    // Only next actions that say nothing about a result have a purpose of their own. This list is code, not prompt
    // wording, so editing a prompt file can never let a result-revealing next action through.
    private static readonly Dictionary<string, string> Purposes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["acknowledge-notification"] = "The care team sent the patient a message and would like to know that it arrived.",
        ["call-patient"] = "The care team will call the patient back, as the patient asked.",
        ["contact-patient-another-way"] = "The care team asks the patient to get in touch.",
        ["rebook-appointment"] = "The care team would like to find a new time for the patient's appointment.",
    };

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

    /// <summary>What the message is for, in a sentence with no result in it.</summary>
    /// <param name="nextAction">The case's next action, or <see langword="null"/>.</param>
    /// <returns>The purpose for one of the few next actions that reveal nothing; otherwise <see cref="NeutralPurpose"/>.</returns>
    public static string PurposeFor(string? nextAction) =>
        nextAction is not null && Purposes.TryGetValue(nextAction.Trim(), out var purpose) ? purpose : NeutralPurpose;

    /// <summary>Builds what the model is given for one case.</summary>
    /// <param name="case">The case.</param>
    /// <param name="channel">Text message or email.</param>
    /// <param name="format">Local dates, times and the clinic's name.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The purpose, the clinic, the channel and an upcoming appointment's time; nothing that identifies the patient.</returns>
    internal static object BuildData(Case @case, NotificationChannel channel, MessageFormat format, DateTimeOffset now) => new
    {
        Channel = channel == NotificationChannel.Sms ? "text message" : "email",
        MaxCharacters = channel == NotificationChannel.Sms ? 459 : 1000,
        Clinic = format.Facility(@case.FacilityId),
        Purpose = PurposeFor(@case.NextAction),
        Appointment = @case.Appointment is { Status: AppointmentStatus.Scheduled, ScheduledFor: { } at } && at >= now
            ? $"{format.Date(at)} at {format.Time(at)}"
            : "none",
    };
}
