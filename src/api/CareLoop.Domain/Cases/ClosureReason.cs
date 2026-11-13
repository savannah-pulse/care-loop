namespace CareLoop.Domain.Cases;

/// <summary>
/// The typed reason a case is closed.
/// </summary>
/// <remarks>
/// Closure must always state why CareLoop may stop watching the patient. The evidence each reason
/// requires is defined per pathway in the pathway definition.
/// </remarks>
public enum ClosureReason
{
    /// <summary>The patient was treated; requires outcome evidence.</summary>
    Treated = 0,

    /// <summary>The patient is on surveillance; requires a next surveillance date.</summary>
    OnSurveillance = 1,

    /// <summary>Confirmation testing was negative; requires the confirming result as evidence.</summary>
    NegativeOnConfirmation = 2,

    /// <summary>Documented exception: the patient declined further follow-up.</summary>
    Declined = 3,

    /// <summary>Documented exception: care was transferred and accepted elsewhere. A referral that was only sent does not qualify.</summary>
    TransferredAndAccepted = 4,

    /// <summary>Documented exception: the patient could not be traced after documented contact attempts.</summary>
    Untraceable = 5,
}
