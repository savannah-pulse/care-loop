namespace CareLoop.Contracts.Summaries;

/// <summary>
/// Whether a handoff summary is still an AI draft or has been approved by a person.
/// </summary>
public enum HandoffSummaryStatus
{
    /// <summary>Drafted by AI and not yet approved. A draft is never sent or published.</summary>
    Draft = 0,

    /// <summary>Approved by a named person; the approval is recorded as an event.</summary>
    Approved = 1,
}
