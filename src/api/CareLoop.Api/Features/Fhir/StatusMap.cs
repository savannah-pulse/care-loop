using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;
using Hl7.Fhir.Model;
using FhirAppointmentStatus = Hl7.Fhir.Model.Appointment.AppointmentStatus;
using FhirTaskStatus = Hl7.Fhir.Model.Task.TaskStatus;

namespace CareLoop.Api.Features.Fhir;

/// <summary>
/// The single place where CareLoop codes are translated to and from FHIR codes.
/// </summary>
/// <remarks>
/// Keeping every code mapping here means a change to the exchange standard touches one file and
/// the mappers stay free of literals. The code system URLs use the reserved <c>.invalid</c>
/// domain until CareLoop has registered code systems; they must be replaced before live exchange.
/// The wire profile these codes belong to is described in <c>tests/api/CareLoop.Tests/Api/Fhir/Samples/README.md</c>.
/// </remarks>
public static class StatusMap
{
    private const string Base = "https://careloop.invalid/fhir";

    /// <summary>Code system for CareLoop pathway stages, used in <c>Task.businessStatus</c>.</summary>
    public const string StageSystem = Base + "/CodeSystem/case-stage";

    /// <summary>Code system for closure and rejection reasons, used in <c>Task.statusReason</c>.</summary>
    public const string StatusReasonSystem = Base + "/CodeSystem/status-reason";

    /// <summary>Code system for the category that distinguishes patient <c>Communication</c>s.</summary>
    public const string CommunicationCategorySystem = Base + "/CodeSystem/communication-category";

    /// <summary>Code system for <c>Task.code</c>: which kind of task a <c>Task</c> is.</summary>
    public const string TaskCodeSystem = Base + "/CodeSystem/task-code";

    /// <summary>Code system for <c>ServiceRequest.category</c>: consultation or referral.</summary>
    public const string ServiceRequestCategorySystem = Base + "/CodeSystem/service-request-category";

    /// <summary>Code system for the pathway a screening result belongs to, in <c>DiagnosticReport.category</c>.</summary>
    public const string PathwaySystem = Base + "/CodeSystem/pathway";

    /// <summary>Code system for outcome evidence kinds (C21), in <c>Procedure.code</c> and the follow-up <c>DiagnosticReport.code</c>.</summary>
    public const string EvidenceKindSystem = Base + "/CodeSystem/evidence-kind";

    /// <summary>Code system for CareLoop roles, carried by the <see cref="ActorRoleExtension"/>.</summary>
    public const string RoleSystem = Base + "/CodeSystem/role";

    /// <summary>Code system for the placeholder urgency a clinician sets on review.</summary>
    public const string UrgencySystem = Base + "/CodeSystem/urgency";

    /// <summary>Code system for <c>Task.output.type</c> on the case <c>Task</c> that closes a case.</summary>
    public const string ClosureOutputSystem = Base + "/CodeSystem/closure-output";

    /// <summary>Identifier system for a queue owner, which has no FHIR resource of its own.</summary>
    public const string QueueIdentifierSystem = Base + "/sid/queue";

    /// <summary>Identifier system for the facility medical record number on <c>Patient.identifier</c>.</summary>
    public const string MrnIdentifierSystem = Base + "/sid/mrn";

    /// <summary>Extension on a <c>Reference</c> to a person or queue: the CareLoop role (<c>valueCode</c>).</summary>
    public const string ActorRoleExtension = Base + "/StructureDefinition/actor-role";

    /// <summary>Extension on a <c>Reference</c> to a person or queue: the facility identifier (<c>valueString</c>).</summary>
    public const string ActorFacilityExtension = Base + "/StructureDefinition/actor-facility";

    /// <summary>
    /// Extension on a resource that has no standard field pointing at the screening result
    /// (the follow-up <c>DiagnosticReport</c>, <c>Consent</c>): <c>valueReference</c> to that <c>DiagnosticReport</c>.
    /// </summary>
    public const string ScreeningResultExtension = Base + "/StructureDefinition/screening-result";

    /// <summary>Extension on the review <c>Task</c>: the placeholder urgency (<c>valueCode</c>) when it does not fit <c>Task.priority</c>.</summary>
    public const string ReviewedUrgencyExtension = Base + "/StructureDefinition/reviewed-urgency";

    /// <summary>Task code of a clinical review task.</summary>
    public const string ClinicalReviewTaskCode = "clinical-review";

    /// <summary>Task code of a consultation task.</summary>
    public const string ConsultationTaskCode = "consultation";

    /// <summary>Task code of a referral task.</summary>
    public const string ReferralTaskCode = "referral";

    /// <summary>Task code of the case task: the case as a whole.</summary>
    public const string CaseTaskCode = "case";

    /// <summary>Task code of an escalation notice.</summary>
    public const string EscalationTaskCode = "escalation";

    /// <summary>Task code of a patient's call-me request passed to the owner.</summary>
    public const string CallRequestTaskCode = "call-request";

    /// <summary>Closure output: reference to the outcome evidence (<c>valueReference</c>).</summary>
    public const string EvidenceOutput = "evidence";

    /// <summary>Closure output: the next surveillance date (<c>valueDate</c>).</summary>
    public const string NextSurveillanceDateOutput = "next-surveillance-date";

    /// <summary>Closure output: a documented contact attempt (<c>valueReference</c>, repeatable).</summary>
    public const string DocumentedAttemptOutput = "documented-attempt";

    /// <summary>Closure output: the explanation when closing on an exception (<c>valueString</c>).</summary>
    public const string ExceptionNoteOutput = "exception-note";

    /// <summary>Communication category code of a delivery report.</summary>
    public const string DeliveryStatusCategory = "delivery-status";

    /// <summary>Communication category code of an acknowledgement.</summary>
    public const string AcknowledgementCategory = "acknowledgement";

    /// <summary>Communication category code of a teach-back answer.</summary>
    public const string TeachBackCategory = "teach-back";

    /// <summary>Communication category code of a call-me request.</summary>
    public const string CallMeCategory = "call-me";

    /// <summary>Communication category code of a confidence rating (C10).</summary>
    public const string ConfidenceRatingCategory = "confidence-rating";

    private static readonly (CaseStage Stage, string Code)[] Stages =
    [
        (CaseStage.Received, "received"),
        (CaseStage.ReviewPending, "review-pending"),
        (CaseStage.FollowUpActive, "follow-up-active"),
        (CaseStage.ClosureReview, "closure-review"),
        (CaseStage.Closed, "closed"),
    ];

    private static readonly (ClosureReason Reason, string Code)[] ClosureReasons =
    [
        (ClosureReason.Treated, "treated"),
        (ClosureReason.OnSurveillance, "on-surveillance"),
        (ClosureReason.NegativeOnConfirmation, "negative-on-confirmation"),
        (ClosureReason.Declined, "declined"),
        (ClosureReason.TransferredAndAccepted, "transferred-and-accepted"),
        (ClosureReason.Untraceable, "untraceable"),
    ];

    /// <summary>Status reason code used when a handoff or referral was rejected; the text carries the reason given.</summary>
    public const string RejectedReasonCode = "rejected";

    /// <summary>
    /// Maps a case's handoff state to <c>Task.status</c>.
    /// </summary>
    /// <param name="case">The case being represented as a FHIR <c>Task</c>.</param>
    /// <returns>The task status: requested while a handoff is pending, accepted or in-progress while owned, rejected after a refusal, completed when closed.</returns>
    public static FhirTaskStatus ToTaskStatus(Case @case)
    {
        ArgumentNullException.ThrowIfNull(@case);

        if (@case.Stage == CaseStage.Closed || @case.Closure is not null)
        {
            return FhirTaskStatus.Completed;
        }

        return @case.PendingAssignment?.Status switch
        {
            AssignmentStatus.Proposed => FhirTaskStatus.Requested,
            AssignmentStatus.Rejected => FhirTaskStatus.Rejected,
            _ when @case.Stage == CaseStage.Received => FhirTaskStatus.Accepted,
            _ => FhirTaskStatus.InProgress,
        };
    }

    /// <summary>
    /// Maps a pathway stage to <c>Task.businessStatus</c>.
    /// </summary>
    /// <param name="stage">The pathway stage.</param>
    /// <returns>A concept in <see cref="StageSystem"/>.</returns>
    public static CodeableConcept ToBusinessStatus(CaseStage stage)
    {
        var code = Stages.Single(s => s.Stage == stage).Code;
        return new CodeableConcept(StageSystem, code, stage.ToString());
    }

    /// <summary>
    /// Reads a pathway stage from <c>Task.businessStatus</c>.
    /// </summary>
    /// <param name="businessStatus">The concept to read.</param>
    /// <returns>The stage, or <see langword="null"/> when the concept carries no known code.</returns>
    public static CaseStage? ToStage(CodeableConcept? businessStatus)
    {
        var code = CodeIn(businessStatus, StageSystem);
        return code is null ? null : Stages.Where(s => s.Code == code).Select(s => (CaseStage?)s.Stage).FirstOrDefault();
    }

    /// <summary>
    /// Maps a closure reason to <c>Task.statusReason</c>.
    /// </summary>
    /// <param name="reason">The typed closure reason.</param>
    /// <returns>A concept in <see cref="StatusReasonSystem"/>.</returns>
    public static CodeableConcept ToStatusReason(ClosureReason reason)
    {
        var code = ClosureReasons.Single(r => r.Reason == reason).Code;
        return new CodeableConcept(StatusReasonSystem, code, reason.ToString());
    }

    /// <summary>
    /// Builds the <c>Task.statusReason</c> of a rejected handoff or referral.
    /// </summary>
    /// <param name="reason">The reason the rejecting party gave.</param>
    /// <returns>A concept in <see cref="StatusReasonSystem"/> whose text is the reason given.</returns>
    public static CodeableConcept ToRejectionReason(string reason) =>
        new(StatusReasonSystem, RejectedReasonCode, "Rejected", reason);

    /// <summary>
    /// Reads a closure reason from <c>Task.statusReason</c>.
    /// </summary>
    /// <param name="statusReason">The concept sent by the source.</param>
    /// <returns>The closure reason, or <see langword="null"/> when the concept carries no known code.</returns>
    public static ClosureReason? ToClosureReason(CodeableConcept? statusReason)
    {
        var code = CodeIn(statusReason, StatusReasonSystem);
        return code is null
            ? null
            : ClosureReasons.Where(r => r.Code == code).Select(r => (ClosureReason?)r.Reason).FirstOrDefault();
    }

    /// <summary>
    /// Maps a consultation <c>Task.status</c> to the consultation track status.
    /// </summary>
    /// <param name="status">The task status sent by the source.</param>
    /// <returns>The track status, or <see langword="null"/> when the status represents no tracked transition.</returns>
    /// <remarks>A <c>requested</c> task that names an owner is a handoff proposal; <c>TaskMapper</c> makes that distinction because it needs the owner.</remarks>
    public static ConsultationStatus? ToConsultationStatus(FhirTaskStatus? status) => status switch
    {
        FhirTaskStatus.Requested => ConsultationStatus.Requested,
        FhirTaskStatus.Accepted => ConsultationStatus.Accepted,
        FhirTaskStatus.Rejected => ConsultationStatus.Rejected,
        FhirTaskStatus.Completed => ConsultationStatus.OutcomeRecorded,
        _ => null,
    };

    /// <summary>
    /// Maps a referral <c>Task.status</c> to the referral track status.
    /// </summary>
    /// <param name="status">The task status sent by the source.</param>
    /// <returns>The track status, or <see langword="null"/> when the status represents no tracked transition.</returns>
    /// <remarks>A completed referral task is deliberately not mapped: a referral is never counted as completed care.</remarks>
    public static ReferralStatus? ToReferralStatus(FhirTaskStatus? status) => status switch
    {
        FhirTaskStatus.Requested => ReferralStatus.Sent,
        FhirTaskStatus.Accepted => ReferralStatus.Accepted,
        FhirTaskStatus.Rejected => ReferralStatus.Rejected,
        _ => null,
    };

    /// <summary>
    /// Maps an <c>Appointment.status</c> to the appointment track status.
    /// </summary>
    /// <param name="status">The appointment status sent by the source.</param>
    /// <returns>The track status, or <see langword="null"/> when the status represents no tracked transition.</returns>
    public static AppointmentStatus? ToAppointmentStatus(FhirAppointmentStatus? status) => status switch
    {
        FhirAppointmentStatus.Booked => AppointmentStatus.Scheduled,
        FhirAppointmentStatus.Fulfilled => AppointmentStatus.Attended,
        FhirAppointmentStatus.Noshow => AppointmentStatus.Missed,
        _ => null,
    };

    /// <summary>
    /// Works out what a <c>Communication</c> reports about a notification.
    /// </summary>
    /// <param name="communication">The communication sent by the patient channel.</param>
    /// <returns>The kind of update, or <see langword="null"/> when the communication is not about a notification.</returns>
    /// <remarks>A delivery report must never be read as an acknowledgement.</remarks>
    public static NotificationUpdateKind? ToNotificationUpdateKind(Communication communication)
    {
        ArgumentNullException.ThrowIfNull(communication);

        var category = communication.Category
            .Select(c => CodeIn(c, CommunicationCategorySystem))
            .FirstOrDefault(c => c is not null);

        return (category, communication.Status) switch
        {
            (DeliveryStatusCategory, EventStatus.Completed) => NotificationUpdateKind.Delivered,
            (DeliveryStatusCategory, EventStatus.NotDone) => NotificationUpdateKind.Failed,
            (DeliveryStatusCategory, _) => null,
            (_, not EventStatus.Completed) => null,
            (AcknowledgementCategory, _) => NotificationUpdateKind.Acknowledged,
            (TeachBackCategory, _) => NotificationUpdateKind.Understood,
            (CallMeCategory, _) => NotificationUpdateKind.CallRequested,
            (ConfidenceRatingCategory, _) => NotificationUpdateKind.ConfidenceRated,
            _ => null,
        };
    }

    /// <summary>
    /// Reads the patient's 1 to 5 rating from a <c>confidence-rating</c> <c>Communication</c> (C10).
    /// </summary>
    /// <param name="communication">The communication sent by the patient channel.</param>
    /// <returns>
    /// The integer in the first <c>payload.contentString</c>, or <see langword="null"/> when there is none.
    /// The range is not checked here: the notification handler refuses a value outside 1 to 5, so the refusal is audited.
    /// </returns>
    public static int? ToConfidence(Communication communication)
    {
        ArgumentNullException.ThrowIfNull(communication);

        foreach (var payload in communication.Payload)
        {
            if (payload.Content is FhirString text && int.TryParse(text.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Maps a FHIR contact point system to the CareLoop notification channel.
    /// </summary>
    /// <param name="system">The <c>ContactPoint.system</c>.</param>
    /// <returns>The channel, or <see langword="null"/> when CareLoop has no provider for that system.</returns>
    /// <remarks><c>other</c> is the simulator inbox, the only channel that exists while only the simulators are connected.</remarks>
    public static NotificationChannel? ToNotificationChannel(Hl7.Fhir.Model.ContactPoint.ContactPointSystem? system) => system switch
    {
        Hl7.Fhir.Model.ContactPoint.ContactPointSystem.Sms => NotificationChannel.Sms,
        Hl7.Fhir.Model.ContactPoint.ContactPointSystem.Email => NotificationChannel.Email,
        Hl7.Fhir.Model.ContactPoint.ContactPointSystem.Other => NotificationChannel.Simulator,
        _ => null,
    };

    /// <summary>
    /// Reads the evidence kind (C21) from a <c>Procedure.code</c> or follow-up <c>DiagnosticReport.code</c>.
    /// </summary>
    /// <param name="code">The concept sent by the source.</param>
    /// <returns>
    /// The kind code exactly as sent in <see cref="EvidenceKindSystem"/>, or <see langword="null"/> when there is none.
    /// Whether the pathway declares the kind is decided at intake, not here.
    /// </returns>
    public static string? ToEvidenceKind(CodeableConcept? code) => CodeIn(code, EvidenceKindSystem);

    /// <summary>
    /// Builds the concept that carries an evidence kind.
    /// </summary>
    /// <param name="kind">The pathway's evidence kind code.</param>
    /// <returns>A concept in <see cref="EvidenceKindSystem"/>.</returns>
    public static CodeableConcept FromEvidenceKind(string kind) => new(EvidenceKindSystem, kind);

    /// <summary>
    /// Builds a <c>Task.code</c> in <see cref="TaskCodeSystem"/>.
    /// </summary>
    /// <param name="code">One of the task code constants.</param>
    /// <returns>The concept.</returns>
    public static CodeableConcept TaskCode(string code) => new(TaskCodeSystem, code);

    /// <summary>
    /// Reads the code of the first coding in a given system.
    /// </summary>
    /// <param name="concept">The concept to read; may be <see langword="null"/>.</param>
    /// <param name="system">The code system to look for.</param>
    /// <returns>The code, or <see langword="null"/> when no coding in that system has one.</returns>
    public static string? CodeIn(CodeableConcept? concept, string system) =>
        concept?.Coding
            .FirstOrDefault(c => string.Equals(c.System, system, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(c.Code))
            ?.Code;
}
