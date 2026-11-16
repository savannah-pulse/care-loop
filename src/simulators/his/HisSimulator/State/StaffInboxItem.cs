namespace HisSimulator.State;

/// <summary>
/// One simulated message waiting for a member of hospital staff.
/// </summary>
/// <remarks>
/// The inbox shows that CareLoop told someone; it never means that person accepted anything.
/// A notification is not an acknowledgement, so receiving an item here changes no ownership
/// and stops no clock in CareLoop.
/// </remarks>
/// <param name="Id">Identifier of the inbox item, assigned by the store.</param>
/// <param name="Kind">Why the item was delivered.</param>
/// <param name="ReceivedAt">When the simulator received it, from <see cref="Time.ISimulatorClock"/>.</param>
/// <param name="RecipientSyntheticId">
/// Synthetic staff identifier (prefixed <c>SYN-</c>) or queue the item is addressed to; <see langword="null"/> if unaddressed.
/// </param>
/// <param name="RecipientRole">Demo role key of the recipient, for example <c>clinician</c> or <c>facility-lead</c>.</param>
/// <param name="Subject">Short display text. For staff notifications this never contains patient detail.</param>
/// <param name="Link">Link back into the (simulated) HIS for the case, when one was supplied.</param>
/// <param name="CareLoopTaskId">Logical id of the CareLoop case <c>Task</c> the item refers to, when known.</param>
/// <param name="FhirResourceType">Resource type that carried the item, or <see langword="null"/> for a direct notification.</param>
/// <param name="LogEntryId">The <see cref="FhirMessageLogEntry"/> holding the raw message, when it arrived as FHIR.</param>
/// <param name="Content">
/// Full text the message carried when that text is the point of the message: the approved handoff summary of a
/// <c>DocumentReference</c>, exactly as CareLoop sent it. <see langword="null"/> for every other kind.
/// </param>
public sealed record StaffInboxItem(
    Guid Id,
    StaffInboxItemKind Kind,
    DateTimeOffset ReceivedAt,
    string? RecipientSyntheticId,
    string? RecipientRole,
    string Subject,
    string? Link,
    string? CareLoopTaskId,
    string? FhirResourceType,
    Guid? LogEntryId,
    string? Content = null)
{
    /// <summary>
    /// Always <see langword="true"/>: marks the item as a simulated message.
    /// </summary>
    public bool Synthetic => true;
}
