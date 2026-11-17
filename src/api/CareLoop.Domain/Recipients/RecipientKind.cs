namespace CareLoop.Domain.Recipients;

/// <summary>
/// Who a notification is addressed to, which decides what content it may carry.
/// </summary>
public enum RecipientKind
{
    /// <summary>The patient the case is about. Receives approved patient templates.</summary>
    Patient = 0,

    /// <summary>A member of staff or a staff queue. Staff notifications carry no patient detail: only "a case needs your action" and the case number.</summary>
    Staff = 1,

    /// <summary>
    /// A family member or other caregiver the patient has consented to involve. Receives only the
    /// appointment date, time and place, never the result, and only while the case's
    /// <see cref="Cases.Consent"/> is active.
    /// </summary>
    Caregiver = 2,
}
