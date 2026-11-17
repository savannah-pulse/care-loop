using Hl7.Fhir.Model;

namespace HisSimulator.Outbound;

/// <summary>
/// How the simulated HIS says a patient can be reached, bound to the <c>Contacts</c> configuration section.
/// </summary>
/// <remarks>
/// CareLoop reaches people by text message and email only, so a contact point is sent with
/// <c>telecom.system</c> <c>sms</c> or <c>email</c>; any other system is not one CareLoop can send to.
/// Scenario files carry no phone number or email address yet, so every patient gets the same kind
/// of contact, chosen here, and the patient's synthetic identifier is sent as the address.
/// </remarks>
public sealed class ContactOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Contacts";

    /// <summary>The <see cref="PatientChannel"/> value for a text-message contact.</summary>
    public const string Sms = "sms";

    /// <summary>The <see cref="PatientChannel"/> value for an email contact.</summary>
    public const string Email = "email";

    /// <summary>How patients are reached: <see cref="Sms"/> (the default) or <see cref="Email"/>.</summary>
    public string PatientChannel { get; set; } = Sms;

    /// <summary>
    /// Tells whether the options name a channel CareLoop can reach a patient on.
    /// </summary>
    /// <param name="options">The options to check.</param>
    /// <returns><see langword="true"/> when <see cref="PatientChannel"/> is <see cref="Sms"/> or <see cref="Email"/>, ignoring case and surrounding white space.</returns>
    public static bool IsValid(ContactOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.PatientChannel?.Trim().ToLowerInvariant() is Sms or Email;
    }

    /// <summary>
    /// The FHIR contact point system for a patient's contact.
    /// </summary>
    /// <returns><c>sms</c> or <c>email</c>.</returns>
    /// <exception cref="InvalidOperationException"><see cref="PatientChannel"/> is neither <see cref="Sms"/> nor <see cref="Email"/>.</exception>
    public ContactPoint.ContactPointSystem PatientSystem() => PatientChannel?.Trim().ToLowerInvariant() switch
    {
        Sms => ContactPoint.ContactPointSystem.Sms,
        Email => ContactPoint.ContactPointSystem.Email,
        _ => throw new InvalidOperationException($"{SectionName}:{nameof(PatientChannel)} must be '{Sms}' or '{Email}': CareLoop reaches patients by text message and email only."),
    };
}
