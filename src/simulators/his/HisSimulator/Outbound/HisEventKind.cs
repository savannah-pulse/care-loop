namespace HisSimulator.Outbound;

/// <summary>
/// The kinds of event the simulated HIS can send to CareLoop.
/// </summary>
/// <remarks>
/// Patient notification updates are absent on purpose: those reach CareLoop through its notification
/// provider, from the patient-channel simulator.
/// </remarks>
public enum HisEventKind
{
    /// <summary>A screening result: <c>DiagnosticReport</c> with <c>Observation</c>s. Opens a case in CareLoop.</summary>
    ScreeningResult,

    /// <summary>A completed clinical review <c>Task</c> carrying the clinician-reviewed urgency extension.</summary>
    ClinicalReview,

    /// <summary>A consultation <c>ServiceRequest</c> plus <c>Task</c>: requested, accepted, rejected or completed.</summary>
    ConsultationUpdate,

    /// <summary>A referral <c>ServiceRequest</c> plus <c>Task</c>: requested, accepted, rejected or completed.</summary>
    ReferralUpdate,

    /// <summary>An <c>Appointment</c>: booked, fulfilled or noshow.</summary>
    AppointmentUpdate,

    /// <summary>An outcome: <c>Procedure</c> or follow-up <c>DiagnosticReport</c>, later used as closure evidence.</summary>
    Outcome,

    /// <summary>A closure decision: CareLoop's case <c>Task</c> set to completed plus a <c>Provenance</c> naming the authorizer.</summary>
    Closure,

    /// <summary>A source-recorded caregiver consent grant or withdrawal.</summary>
    ConsentRecorded,
}
