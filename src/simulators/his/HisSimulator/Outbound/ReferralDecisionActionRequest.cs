namespace HisSimulator.Outbound;

/// <summary>
/// Body of <c>POST /actions/referral-decision</c>: the receiving clinician accepts or rejects a referral.
/// </summary>
/// <param name="CaseRef">Simulator-side handle of the case the referral belongs to.</param>
/// <param name="Performer">The synthetic clinician making the decision.</param>
/// <param name="Accepted">
/// <see langword="true"/> to accept the referral; <see langword="false"/> to reject it. Required: a body that omits it is
/// refused with 400, so the simulator never sends a decision no person stated.
/// </param>
/// <param name="Reason">Free-text or coded reason, expected when the referral is rejected; sent as <c>Task.statusReason</c>.</param>
public sealed record ReferralDecisionActionRequest(string CaseRef, ActingStaff Performer, bool? Accepted, string? Reason);
