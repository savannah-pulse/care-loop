namespace CareLoop.Domain.Tracks;

/// <summary>
/// Status of the referral track, taken from the FHIR referral <c>Task</c>.
/// </summary>
public enum ReferralStatus
{
    /// <summary>The referral was sent. Not completed care.</summary>
    Sent = 0,

    /// <summary>The receiving service accepted the referral. Completes the track but is still not completed care.</summary>
    Accepted = 1,

    /// <summary>The receiving service rejected the referral; the current owner must arrange an alternative.</summary>
    Rejected = 2,
}
