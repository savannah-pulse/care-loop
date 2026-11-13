namespace CareLoop.Domain.Cases;

/// <summary>
/// The pathway stage a case is in.
/// </summary>
/// <remarks>
/// The entry and exit rules for each stage live in the pathway definition, not in code, because
/// they are still to be confirmed by the team. <c>Escalated</c> is deliberately not a stage: it is
/// a flag on the case, so an escalated case keeps its place in the pathway.
/// </remarks>
public enum CaseStage
{
    /// <summary>The result event was ingested and deduplicated. Leaves when an owner is assigned or the case lands in the unassigned queue.</summary>
    Received = 0,

    /// <summary>Awaiting clinician review of urgency and follow-up. Leaves when a review event arrives.</summary>
    ReviewPending = 1,

    /// <summary>The parallel tracks (notification, consultation, referral, appointment) are in progress. Leaves when an outcome event is recorded.</summary>
    FollowUpActive = 2,

    /// <summary>Outcome evidence is present and an authorized closure decision is awaited.</summary>
    ClosureReview = 3,

    /// <summary>Final. A closed case never leaves this stage.</summary>
    Closed = 4,
}
