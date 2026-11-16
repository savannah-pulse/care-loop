namespace HisSimulator.State;

/// <summary>
/// Why an item was placed in the simulated staff inbox.
/// </summary>
/// <remarks>
/// Each kind corresponds to one CareLoop domain event that reaches staff, either as a FHIR
/// resource or as a direct staff notification.
/// </remarks>
public enum StaffInboxItemKind
{
    /// <summary>An escalation notice (<c>Task</c>) raised because a deadline passed without action.</summary>
    Escalation,

    /// <summary>An ownership handoff proposed to a member of staff (<c>Task</c>), awaiting accept or reject.</summary>
    HandoffProposed,

    /// <summary>An ownership handoff proposal was rejected and the case returns to its current owner (<c>Task</c> with status <c>rejected</c>).</summary>
    HandoffRejected,

    /// <summary>
    /// A referral was rejected and its next action returns to the current owner (case <c>Task</c> still in progress,
    /// with a <c>rejected</c> status reason).
    /// </summary>
    ReferralRejected,

    /// <summary>A patient asked to be called (<c>Task</c>) and the current owner must act.</summary>
    CallRequested,

    /// <summary>A handoff summary document (<c>DocumentReference</c>) accompanying a proposed handoff.</summary>
    HandoffSummary,

    /// <summary>
    /// A direct staff notification: "a case needs your action" plus a link, carrying no patient detail.
    /// </summary>
    StaffNotification,
}
