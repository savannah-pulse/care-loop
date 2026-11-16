using HisSimulator.State;

namespace HisSimulator.Outbound;

/// <summary>
/// One case in <c>GET /cases</c>: the simulated HIS record plus what the HIS has since received about it.
/// </summary>
/// <remarks>
/// <see cref="ApprovedHandoffSummary"/> is read-only context for the person acting in the HIS. It is not part of
/// <see cref="SimulatedCase"/>, which stays the HIS's own record of what it sent.
/// </remarks>
/// <param name="CaseRef">Simulator-side handle of the case.</param>
/// <param name="Patient">The synthetic patient, including the display name and MRN shown to staff.</param>
/// <param name="FacilityId">Synthetic facility that produced the result.</param>
/// <param name="PathwayId">Identifier of the pathway definition the case runs against.</param>
/// <param name="DiagnosticReportId">Logical id of the screening <c>DiagnosticReport</c> sent to CareLoop.</param>
/// <param name="CareLoopTaskId">Logical id of CareLoop's case <c>Task</c>, once learned; otherwise <see langword="null"/>.</param>
/// <param name="ResourceIds">Logical ids and references of the other resources sent for the case, keyed by purpose.</param>
/// <param name="ScenarioRunId">The scenario run that created the case, or <see langword="null"/> if it was created manually.</param>
/// <param name="ApprovedHandoffSummary">
/// The approved handoff summary, exactly as CareLoop sent it in a <c>DocumentReference</c>, or <see langword="null"/>
/// when none has been received for the case.
/// </param>
public sealed record SimulatedCaseItem(
    string CaseRef,
    SimulatedPatient Patient,
    string FacilityId,
    string PathwayId,
    string DiagnosticReportId,
    string? CareLoopTaskId,
    IReadOnlyDictionary<string, string> ResourceIds,
    Guid? ScenarioRunId,
    string? ApprovedHandoffSummary)
{
    /// <summary>
    /// Always <see langword="true"/>: marks the case as simulated.
    /// </summary>
    public bool Synthetic => true;

    /// <summary>
    /// Projects a stored case for the read endpoint.
    /// </summary>
    /// <param name="case">The HIS record of the case.</param>
    /// <param name="approvedHandoffSummary">The approved summary received for the case, if any.</param>
    /// <returns>The read model.</returns>
    public static SimulatedCaseItem From(SimulatedCase @case, string? approvedHandoffSummary) =>
        new(@case.CaseRef, @case.Patient, @case.FacilityId, @case.PathwayId, @case.DiagnosticReportId, @case.CareLoopTaskId,
            @case.ResourceIds, @case.ScenarioRunId, approvedHandoffSummary);
}
