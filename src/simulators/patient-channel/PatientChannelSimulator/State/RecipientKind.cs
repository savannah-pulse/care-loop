namespace PatientChannelSimulator.State;

/// <summary>
/// Who an inbox message was sent to.
/// </summary>
/// <remarks>
/// CareLoop marks each <c>CommunicationRequest</c> with a recipient kind. The distinction is
/// safety-relevant: the case's notification track follows the patient only, so nothing a
/// caregiver does may ever count as the patient's delivery or acknowledgement.
/// </remarks>
public enum RecipientKind
{
    /// <summary>The message is for the patient. Only these messages can be answered.</summary>
    Patient,

    /// <summary>The message is a caregiver's copy (C13). It is shown in the Email inbox and is read-only.</summary>
    Caregiver,
}
