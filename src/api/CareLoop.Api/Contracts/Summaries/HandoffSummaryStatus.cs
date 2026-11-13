namespace CareLoop.Contracts.Summaries;

/// <summary>
/// Where a handoff summary stands with its reviewer.
/// </summary>
/// <remarks>
/// Three states, not two: a draft a reviewer turned down must be distinguishable from one nobody
/// has read, both for the person looking at the case and for the evaluation question of how often
/// reviewers reject what AI writes.
/// </remarks>
public enum HandoffSummaryStatus
{
    /// <summary>Drafted by AI and not yet reviewed. A draft is never sent or published.</summary>
    Draft = 0,

    /// <summary>Approved by a named person; the approval is recorded as an event.</summary>
    Approved = 1,

    /// <summary>Turned down by a named reviewer with a reason. Never sent, and never silently redrafted.</summary>
    Rejected = 2,
}
