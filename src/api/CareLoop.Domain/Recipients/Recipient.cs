namespace CareLoop.Domain.Recipients;

/// <summary>
/// A person or staff queue that CareLoop can notify: the addressee of a notification.
/// </summary>
/// <remarks>
/// <para>
/// <b>Definition.</b> A recipient is who a message goes to and how they can be reached. It is
/// either the patient a case is about or a member of staff (or a staff queue), as stated by
/// <see cref="Kind"/>.
/// </para>
/// <para><b>What a recipient holds.</b></para>
/// <list type="bullet">
/// <item><description><see cref="Reference"/>: the patient or staff reference in the hospital system.</description></item>
/// <item><description><see cref="Kind"/>: patient or staff, which decides what content the message may carry. Staff notifications carry no patient detail.</description></item>
/// <item><description><see cref="Language"/>: the language key used to pick the message template.</description></item>
/// <item><description><see cref="ContactPoints"/>: one address per channel on which the recipient can be reached.</description></item>
/// </list>
/// <para>
/// <b>What it is not.</b> A recipient is not an owner. Notifying a recipient never transfers
/// ownership of a case and never stops a deadline clock: a notification is not an acknowledgement.
/// Nor is it a channel preference: which channels are used is system configuration, and a message
/// is sent on every enabled channel that can reach the recipient.
/// </para>
/// <para>
/// <b>Where it comes from.</b> A case stores no contact data. A recipient is resolved from a
/// reference at the moment a notification is sent, so contact details are never copied into case
/// state.
/// </para>
/// </remarks>
public sealed record Recipient
{
    /// <summary>Source-system reference for the person or queue (for example a FHIR <c>Patient</c> or <c>Practitioner</c> reference). Never a name.</summary>
    public required string Reference { get; init; }

    /// <summary>Whether this is the patient or a member of staff; decides which templates are allowed.</summary>
    public required RecipientKind Kind { get; init; }

    /// <summary>BCP 47 language tag used to select the message template. English only for now.</summary>
    public required string Language { get; init; }

    /// <summary>Every known way of reaching the recipient. A provider can reach the recipient only if a contact point exists for its channel.</summary>
    public required IReadOnlyList<ContactPoint> ContactPoints { get; init; }
}
