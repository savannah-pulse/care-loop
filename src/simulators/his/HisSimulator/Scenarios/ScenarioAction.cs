namespace HisSimulator.Scenarios;

/// <summary>
/// The kind of event a scenario step emits; one value per event type a hospital system sends to CareLoop.
/// Serialized in lower-case kebab form, for example <c>screening-result</c>.
/// </summary>
public enum ScenarioAction
{
    /// <summary>A new screening result (<c>DiagnosticReport</c> with <c>Observation</c>s) that opens a case.</summary>
    ScreeningResult,

    /// <summary>A completed clinical review (<c>Task</c>) carrying the clinician-reviewed urgency.</summary>
    ClinicalReview,

    /// <summary>A consultation request or its status change (<c>ServiceRequest</c> plus <c>Task</c>).</summary>
    ConsultationUpdate,

    /// <summary>A referral or its status change (<c>ServiceRequest</c> plus <c>Task</c>).</summary>
    ReferralUpdate,

    /// <summary>An appointment booking, attendance or no-show (<c>Appointment</c>).</summary>
    AppointmentUpdate,

    /// <summary>A recorded outcome (<c>Procedure</c> or follow-up <c>DiagnosticReport</c>) that becomes closure evidence.</summary>
    Outcome,

    /// <summary>An authorized closure decision (case <c>Task</c> completed plus <c>Provenance</c>).</summary>
    Closure,
}
