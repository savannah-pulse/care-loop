namespace CareLoop.Domain.Rules;

/// <summary>
/// Where a case's next action stands against its deadline.
/// </summary>
public enum DeadlineStatus
{
    /// <summary>The case has no deadline (it is closed) or the deadline is comfortably ahead.</summary>
    OnTrack = 0,

    /// <summary>The deadline is within the pathway's warning window; the owner should be reminded.</summary>
    Approaching = 1,

    /// <summary>The deadline has passed; the case is overdue and may need escalating.</summary>
    Overdue = 2,
}
