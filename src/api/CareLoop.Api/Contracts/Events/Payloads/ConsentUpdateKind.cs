namespace CareLoop.Contracts.Events.Payloads;

/// <summary>
/// What a <c>Consent</c> resource about a caregiver reports.
/// </summary>
public enum ConsentUpdateKind
{
    /// <summary>The patient agreed for the named caregiver to be told about appointments.</summary>
    Granted = 0,

    /// <summary>The patient withdrew that agreement. No further caregiver message may be sent.</summary>
    Withdrawn = 1,
}
