namespace PatientChannelSimulator.Outbound;

/// <summary>
/// The patient-facing view of a case, derived from CareLoop's case <c>Task</c>.
/// </summary>
/// <remarks>
/// Coordination status only: where the follow-up stands and by when the next step is due. It
/// carries no result, diagnosis or urgency, because the patient channel is not a clinical system.
/// </remarks>
/// <param name="TaskId">Logical id of the case <c>Task</c> in CareLoop.</param>
/// <param name="TaskStatus">The FHIR <c>Task.status</c> code (handoff status).</param>
/// <param name="Stage">The pathway stage, from <c>Task.businessStatus</c>; <see langword="null"/> when absent.</param>
/// <param name="StatusReason">Rejection or closure reason, from <c>Task.statusReason</c>; <see langword="null"/> when absent.</param>
/// <param name="DueBy">End of <c>Task.restriction.period</c>: the current deadline; <see langword="null"/> when absent.</param>
/// <param name="LastUpdated">When CareLoop last changed the task, from <c>Task.lastModified</c> or <c>meta.lastUpdated</c>.</param>
/// <param name="Simulated">Visible simulated-data marker; always <see langword="true"/>.</param>
public sealed record PatientCaseStatus(
    string TaskId,
    string TaskStatus,
    string? Stage,
    string? StatusReason,
    DateTimeOffset? DueBy,
    DateTimeOffset? LastUpdated,
    bool Simulated = true);
