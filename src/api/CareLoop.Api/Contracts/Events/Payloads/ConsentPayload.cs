using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;

namespace CareLoop.Contracts.Events.Payloads;

/// <summary>
/// Payload of <see cref="CaseEventType.ConsentRecorded"/>: the patient granted or withdrew consent for a caregiver.
/// </summary>
/// <remarks>
/// Consent is its own event rather than a field on the screening result because it has a life of its
/// own: it can be granted after the case opened and withdrawn at any time, each with its own evidence
/// and its own audit entry. It carries the caregiver's contact point and a reference to the consent
/// record, never the consent text or the caregiver's name.
/// </remarks>
public sealed record ConsentPayload : EventPayload
{
    /// <summary>Whether the consent was granted or withdrawn.</summary>
    public required ConsentUpdateKind Kind { get; init; }

    /// <summary>Reference to the source <c>Consent</c> resource, which is the evidence for the grant or the withdrawal.</summary>
    public required string ConsentReference { get; init; }

    /// <summary>How the caregiver is reached. Required when <see cref="Kind"/> is <see cref="ConsentUpdateKind.Granted"/>; ignored on withdrawal.</summary>
    public ContactPoint? CaregiverContactPoint { get; init; }
}
