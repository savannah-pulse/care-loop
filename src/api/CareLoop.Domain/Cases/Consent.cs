using CareLoop.Domain.Recipients;

namespace CareLoop.Domain.Cases;

/// <summary>
/// The patient's consent for a caregiver to be told about their appointments.
/// </summary>
/// <remarks>
/// <para>
/// <b>Definition.</b> A record that the patient agreed, with evidence, for one caregiver to receive
/// appointment reminders (date, time and place only, never the result). It is active from
/// <see cref="GrantedAt"/> until <see cref="WithdrawnAt"/> is set; withdrawal stops every further
/// caregiver message.
/// </para>
/// <para>
/// <b>Why the contact point is here.</b> A case otherwise stores no contact data. The caregiver's
/// contact point is the one exception, because it exists only by the patient's consent and must
/// disappear with it: binding the two in one record means a message can never be sent to a
/// caregiver without an active consent to point at.
/// </para>
/// <para>
/// <b>What it is not.</b> Consent is not a clinical decision and does not change ownership, stage or
/// any deadline. CareLoop never infers it: it is recorded only from an event that carries evidence.
/// </para>
/// </remarks>
public sealed record Consent
{
    /// <summary>How the caregiver is reached. Personal data; held only while the consent exists.</summary>
    public required ContactPoint CaregiverContactPoint { get; init; }

    /// <summary>When the patient granted the consent according to the source.</summary>
    public required DateTimeOffset GrantedAt { get; init; }

    /// <summary>When the consent was withdrawn, or <see langword="null"/> while it is active.</summary>
    public DateTimeOffset? WithdrawnAt { get; init; }

    /// <summary>Reference to the record of the patient's consent in the source system (for example a FHIR <c>Consent</c>). Never the consent text itself.</summary>
    public required string EvidenceReference { get; init; }

    /// <summary>Reference to the record of the withdrawal, when the consent has been withdrawn with evidence.</summary>
    public string? WithdrawalEvidenceReference { get; init; }

    /// <summary><see langword="true"/> while the consent has not been withdrawn; a caregiver may be contacted only then.</summary>
    public bool IsActive => WithdrawnAt is null;
}
