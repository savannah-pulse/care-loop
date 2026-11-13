namespace CareLoop.Domain.Cases;

/// <summary>
/// The unresolved exceptions currently flagged on a case.
/// </summary>
/// <remarks>
/// A case with any flag set appears in the exception queue. An exception never parks a case:
/// whenever a flag is set the case must also have a next action, an owner and a deadline.
/// </remarks>
[Flags]
public enum CaseExceptions
{
    /// <summary>No unresolved exception.</summary>
    None = 0,

    /// <summary>The current next action is past its deadline.</summary>
    Overdue = 1,

    /// <summary>A handoff or referral was rejected and the alternative has not yet been arranged.</summary>
    Rejected = 2,

    /// <summary>Every notification channel failed, so the patient could not be reached.</summary>
    Unreachable = 4,

    /// <summary>The notification was delivered but the patient has not acknowledged it in time.</summary>
    Unacknowledged = 8,

    /// <summary>The case has no named owner and sits in the facility lead queue.</summary>
    Unassigned = 16,
}
