using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;

namespace CareLoop.Contracts.Events.Payloads;

/// <summary>
/// Payload of <see cref="CaseEventType.ScreeningResultReceived"/>: an actionable screening result that opens a case.
/// </summary>
public sealed record ScreeningResultPayload : EventPayload
{
    /// <summary>Source-system reference of the patient. Never a name.</summary>
    public required string PatientReference { get; init; }

    /// <summary>
    /// The patient's display name as the source system shows it, or <see langword="null"/> when the
    /// source did not supply one. Demographic, not clinical: it lets staff recognise who a case is
    /// about and is returned to staff roles only, never to a patient-facing or system channel.
    /// </summary>
    public string? PatientDisplayName { get; init; }

    /// <summary>
    /// A second identifier of the patient, typically the medical record number (MRN) of the facility,
    /// or <see langword="null"/> when the source did not supply one. Demographic, not clinical; shown
    /// to staff roles only so they can match the case to the record in front of them.
    /// </summary>
    public string? PatientSecondaryIdentifier { get; init; }

    /// <summary>Identifier of the pathway the result belongs to.</summary>
    public required string PathwayId { get; init; }

    /// <summary>The facility that produced the result.</summary>
    public required string FacilityId { get; init; }

    /// <summary>Reference to the source <c>DiagnosticReport</c>.</summary>
    public required string ResultReference { get; init; }

    /// <summary>The responsible person named by the source, or <see langword="null"/> when none was named (the case then goes to the facility lead queue).</summary>
    public Actor? NamedOwner { get; init; }

    /// <summary>The language used for patient messages, as a BCP 47 tag.</summary>
    public required string PatientLanguage { get; init; }

    /// <summary>The ways the patient can be reached, as supplied by the source system. Phone numbers for text messages and email addresses; synthetic ones while only the simulators are connected.</summary>
    public IReadOnlyList<ContactPoint> PatientContactPoints { get; init; } = [];
}
