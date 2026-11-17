namespace PatientChannelSimulator.State;

/// <summary>
/// Who a simulated message is for.
/// </summary>
/// <remarks>
/// Read from the <c>audience</c> CareLoop puts in a message's metadata. Only the patient's own
/// reply counts, so a caregiver's or a staff member's message is shown and can never be replied to.
/// </remarks>
public enum RecipientKind
{
    /// <summary>The patient the case is about. The only kind of message that can be replied to.</summary>
    Patient,

    /// <summary>A family member or other caregiver the patient consented to involve. They get the day, time and place of an appointment only.</summary>
    Caregiver,

    /// <summary>A member of staff or a staff role. A staff notice carries no patient detail.</summary>
    Staff,
}
