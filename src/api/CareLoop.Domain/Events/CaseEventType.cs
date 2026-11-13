namespace CareLoop.Domain.Events;

/// <summary>
/// The kinds of event that can change a case.
/// </summary>
/// <remarks>
/// The first eight members are source events that arrive through the FHIR boundary and each has
/// exactly one event handler. The remaining members are raised inside CareLoop (by the deadline
/// worker or the dashboard) and are persisted through the same single write path so the audit
/// trail stays complete.
/// </remarks>
public enum CaseEventType
{
    /// <summary>An actionable screening result arrived (FHIR <c>DiagnosticReport</c>). Opens a case.</summary>
    ScreeningResultReceived = 0,

    /// <summary>A clinician completed the review and set the urgency (FHIR review <c>Task</c>). CareLoop never sets urgency itself.</summary>
    ClinicalReviewCompleted = 1,

    /// <summary>The notification track changed: delivery, failure, acknowledgement, teach-back or a call-me request (FHIR <c>Communication</c>).</summary>
    NotificationUpdated = 2,

    /// <summary>The consultation track changed, including proposed, accepted or rejected ownership (FHIR <c>ServiceRequest</c> plus <c>Task</c>).</summary>
    ConsultationUpdated = 3,

    /// <summary>The referral track changed: sent, accepted or rejected (FHIR <c>ServiceRequest</c> plus <c>Task</c>).</summary>
    ReferralUpdated = 4,

    /// <summary>The appointment track changed: scheduled, attended or missed (FHIR <c>Appointment</c>).</summary>
    AppointmentUpdated = 5,

    /// <summary>Outcome evidence was recorded (FHIR <c>Procedure</c> or follow-up <c>DiagnosticReport</c>).</summary>
    OutcomeRecorded = 6,

    /// <summary>An authorized person decided to close the case (case <c>Task</c> completed plus <c>Provenance</c>).</summary>
    ClosureDecided = 7,

    /// <summary>Internal: a deadline was approaching or passed and the deadline worker warned or escalated.</summary>
    DeadlineElapsed = 100,

    /// <summary>Internal: a person approved an AI-drafted handoff summary in the dashboard.</summary>
    HandoffSummaryApproved = 101,
}
