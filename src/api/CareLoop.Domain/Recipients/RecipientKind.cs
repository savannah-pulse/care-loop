namespace CareLoop.Domain.Recipients;

/// <summary>
/// Who a notification is addressed to, which decides what content it may carry.
/// </summary>
public enum RecipientKind
{
    /// <summary>The patient the case is about. Receives approved patient templates.</summary>
    Patient = 0,

    /// <summary>A member of staff or a staff queue. Staff notifications carry no patient detail: only "a case needs your action" and a link into the HIS.</summary>
    Staff = 1,
}
