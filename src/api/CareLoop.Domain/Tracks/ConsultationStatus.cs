namespace CareLoop.Domain.Tracks;

/// <summary>
/// Status of the consultation track, taken from the FHIR consultation <c>Task</c>.
/// </summary>
public enum ConsultationStatus
{
    /// <summary>A consultation was requested.</summary>
    Requested = 0,

    /// <summary>Ownership was proposed to the consulting clinician; the previous owner is still accountable.</summary>
    OwnerProposed = 1,

    /// <summary>The consulting clinician accepted and now owns the case.</summary>
    Accepted = 2,

    /// <summary>The consulting clinician refused; the next action returned to the current owner.</summary>
    Rejected = 3,

    /// <summary>The consultation outcome was recorded. Completes the track.</summary>
    OutcomeRecorded = 4,
}
