namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// What caused a draft to be produced.
/// </summary>
/// <remarks>
/// Stored with the draft because the two triggers carry different accountability: a person asked
/// for one of them. An automatic draft is still only a draft, and the audit trail has to show that
/// no person had yet seen it when it was produced.
/// </remarks>
public enum DraftTrigger
{
    /// <summary>A person asked for the draft on the case screen.</summary>
    Requested = 0,

    /// <summary>A handoff was proposed and the draft was produced without anyone asking (decision 11).</summary>
    HandoffProposed = 1,
}
