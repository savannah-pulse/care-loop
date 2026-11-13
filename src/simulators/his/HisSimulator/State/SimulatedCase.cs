namespace HisSimulator.State;

/// <summary>
/// The simulated HIS's own record of one actionable screening result and the FHIR resources it has sent for it.
/// </summary>
/// <remarks>
/// This is the hospital side of the story, not CareLoop's case projection. It exists so later
/// events (review, referral, outcome, closure) can reference the identifiers of resources the
/// simulator sent earlier, exactly as a real HIS would keep its own keys.
/// </remarks>
/// <param name="CaseRef">Local handle for the case: a scenario step's <c>caseRef</c>, qualified by the run when scripted.</param>
/// <param name="Patient">The synthetic patient the result belongs to.</param>
/// <param name="FacilityId">Synthetic facility identifier (prefixed <c>SYN-</c>) that produced the result.</param>
/// <param name="PathwayId">Identifier of the pathway definition the case runs against.</param>
/// <param name="DiagnosticReportId">Logical id of the screening <c>DiagnosticReport</c> sent to CareLoop.</param>
/// <param name="CareLoopTaskId">Logical id of CareLoop's case <c>Task</c>, once learned; otherwise <see langword="null"/>.</param>
/// <param name="ResourceIds">
/// Logical ids of other resources sent for this case, keyed by purpose (for example <c>referral-service-request</c>,
/// <c>outcome</c>), so later events can reference them.
/// </param>
/// <param name="ScenarioRunId">The scenario run that created the case, or <see langword="null"/> if it was created manually.</param>
public sealed record SimulatedCase(
    string CaseRef,
    SimulatedPatient Patient,
    string FacilityId,
    string PathwayId,
    string DiagnosticReportId,
    string? CareLoopTaskId,
    IReadOnlyDictionary<string, string> ResourceIds,
    Guid? ScenarioRunId)
{
    /// <summary>
    /// Always <see langword="true"/>: marks the case as simulated.
    /// </summary>
    public bool Synthetic => true;
}
