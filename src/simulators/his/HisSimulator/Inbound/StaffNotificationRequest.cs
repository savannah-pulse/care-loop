namespace HisSimulator.Inbound;

/// <summary>
/// Body of <c>POST /staff-inbox/notifications</c>: a direct staff notification from CareLoop's simulator notification provider.
/// </summary>
/// <remarks>
/// Staff notifications carry no patient detail: only "a case needs your action" plus a link into the HIS.
/// The simulator rejects nothing on content grounds, but the fields offered here deliberately leave no
/// place for patient information.
/// </remarks>
/// <param name="RecipientSyntheticId">Synthetic staff identifier or queue (prefixed <c>SYN-</c>) the notification is for.</param>
/// <param name="RecipientRole">Demo role key of the recipient, for example <c>clinician</c> or <c>facility-lead</c>.</param>
/// <param name="Message">The rendered staff template text, for example "A case needs your action".</param>
/// <param name="Link">Link into the (simulated) HIS for the case.</param>
/// <param name="CareLoopTaskId">Logical id of CareLoop's case <c>Task</c>, so the item can be correlated without patient detail.</param>
/// <param name="EventType">Name of the CareLoop domain event that caused the notification, for example <c>DeadlineApproaching</c>.</param>
public sealed record StaffNotificationRequest(
    string RecipientSyntheticId,
    string? RecipientRole,
    string Message,
    string? Link,
    string? CareLoopTaskId,
    string? EventType);
