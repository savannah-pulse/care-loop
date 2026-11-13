using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
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
/// </remarks>
public static class StatusMap
{
    /// <summary>Code system for CareLoop pathway stages, used in <c>Task.businessStatus</c>.</summary>
    public const string StageSystem = "https://careloop.invalid/fhir/CodeSystem/case-stage";

    /// <summary>Code system for closure and rejection reasons, used in <c>Task.statusReason</c>.</summary>
    public const string StatusReasonSystem = "https://careloop.invalid/fhir/CodeSystem/status-reason";

    /// <summary>Code system for the category that distinguishes patient <c>Communication</c>s.</summary>
    public const string CommunicationCategorySystem = "https://careloop.invalid/fhir/CodeSystem/communication-category";

    /// <summary>
    /// Maps a case's handoff state to <c>Task.status</c>.
    /// </summary>
    /// <param name="case">The case being represented as a FHIR <c>Task</c>.</param>
    /// <returns>The task status: requested while a handoff is pending, accepted or in-progress while owned, rejected after a refusal, completed when closed.</returns>
    public static FhirTaskStatus ToTaskStatus(Case @case)
    {
        // TODO(WS-3): derive Task.status from PendingAssignment, Stage and Closure.
        throw new NotImplementedException("WS3: StatusMap.ToTaskStatus");
    }

    /// <summary>
    /// Maps a pathway stage to <c>Task.businessStatus</c>.
    /// </summary>
    /// <param name="stage">The pathway stage.</param>
    /// <returns>A concept in <see cref="StageSystem"/>.</returns>
    public static CodeableConcept ToBusinessStatus(CaseStage stage)
    {
        // TODO(WS-3): build a CodeableConcept in StageSystem.
        throw new NotImplementedException("WS3: StatusMap.ToBusinessStatus");
    }

    /// <summary>
    /// Maps a closure reason to <c>Task.statusReason</c>.
    /// </summary>
    /// <param name="reason">The typed closure reason.</param>
    /// <returns>A concept in <see cref="StatusReasonSystem"/>.</returns>
    public static CodeableConcept ToStatusReason(ClosureReason reason)
    {
        // TODO(WS-3): build a CodeableConcept in StatusReasonSystem.
        throw new NotImplementedException("WS3: StatusMap.ToStatusReason");
    }

    /// <summary>
    /// Reads a closure reason from <c>Task.statusReason</c>.
    /// </summary>
    /// <param name="statusReason">The concept sent by the source.</param>
    /// <returns>The closure reason, or <see langword="null"/> when the concept carries no known code.</returns>
    public static ClosureReason? ToClosureReason(CodeableConcept? statusReason)
    {
        // TODO(WS-3): look up the StatusReasonSystem coding.
        throw new NotImplementedException("WS3: StatusMap.ToClosureReason");
    }

    /// <summary>
    /// Maps a consultation <c>Task.status</c> to the consultation track status.
    /// </summary>
    /// <param name="status">The task status sent by the source.</param>
    /// <returns>The track status, or <see langword="null"/> when the status represents no tracked transition.</returns>
    public static ConsultationStatus? ToConsultationStatus(FhirTaskStatus? status)
    {
        // TODO(WS-3): requested, accepted, rejected, completed.
        throw new NotImplementedException("WS3: StatusMap.ToConsultationStatus");
    }

    /// <summary>
    /// Maps a referral <c>Task.status</c> to the referral track status.
    /// </summary>
    /// <param name="status">The task status sent by the source.</param>
    /// <returns>The track status, or <see langword="null"/> when the status represents no tracked transition.</returns>
    public static ReferralStatus? ToReferralStatus(FhirTaskStatus? status)
    {
        // TODO(WS-3): requested maps to Sent; accepted; rejected.
        throw new NotImplementedException("WS3: StatusMap.ToReferralStatus");
    }

    /// <summary>
    /// Maps an <c>Appointment.status</c> to the appointment track status.
    /// </summary>
    /// <param name="status">The appointment status sent by the source.</param>
    /// <returns>The track status, or <see langword="null"/> when the status represents no tracked transition.</returns>
    public static AppointmentStatus? ToAppointmentStatus(FhirAppointmentStatus? status)
    {
        // TODO(WS-3): booked, fulfilled, noshow.
        throw new NotImplementedException("WS3: StatusMap.ToAppointmentStatus");
    }

    /// <summary>
    /// Works out what a <c>Communication</c> reports about a notification.
    /// </summary>
    /// <param name="communication">The communication sent by the patient channel.</param>
    /// <returns>The kind of update, or <see langword="null"/> when the communication is not about a notification.</returns>
    /// <remarks>A delivery report must never be read as an acknowledgement.</remarks>
    public static NotificationUpdateKind? ToNotificationUpdateKind(Communication communication)
    {
        // TODO(WS-3): use status (completed / not-done) and the CommunicationCategorySystem category.
        throw new NotImplementedException("WS3: StatusMap.ToNotificationUpdateKind");
    }
}
