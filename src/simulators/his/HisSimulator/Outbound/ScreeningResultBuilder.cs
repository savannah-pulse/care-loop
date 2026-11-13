using HisSimulator.State;
using Hl7.Fhir.Model;

namespace HisSimulator.Outbound;

/// <summary>
/// Builds the FHIR representation of a screening result: a <c>DiagnosticReport</c> with its <c>Observation</c>s.
/// </summary>
/// <remarks>
/// A new report opens a case in CareLoop. The report carries references and placeholder codes only;
/// the result category comes from <c>Params["resultCategory"]</c> and is never invented here.
/// </remarks>
public sealed class ScreeningResultBuilder
{
    /// <summary>
    /// Builds the screening <c>DiagnosticReport</c> for a new case.
    /// </summary>
    /// <param name="request">
    /// The event to represent; <see cref="OutboundEventRequest.Patient"/> and the <c>resultCategory</c>
    /// and <c>facility</c> params are required.
    /// </param>
    /// <returns>
    /// A final <c>DiagnosticReport</c> with a deterministic logical id derived from
    /// <see cref="OutboundEventRequest.SourceEventKey"/>, the synthetic patient as subject, the facility
    /// as performer, the placeholder result category as conclusion code, its <c>Observation</c>s as
    /// contained resources referenced from <c>result</c>, and a simulated-data tag in <c>meta.tag</c>.
    /// </returns>
    /// <exception cref="ArgumentException">A required value (patient, result category or facility) is missing.</exception>
    public DiagnosticReport Build(OutboundEventRequest request)
    {
        // TODO(WS-7): build the DiagnosticReport and contained Observations with Firely model classes.
        // Confirm with WS6 whether Observations travel contained (assumed) or as separate POSTs.
        throw new NotImplementedException("WS7: ScreeningResultBuilder.Build");
    }

    /// <summary>
    /// Creates the simulator's own record of the case opened by a screening result.
    /// </summary>
    /// <param name="request">The screening-result event.</param>
    /// <param name="report">The report built for it by <see cref="Build"/>.</param>
    /// <returns>A <see cref="SimulatedCase"/> holding the patient, facility, pathway and report id for later events.</returns>
    public SimulatedCase CreateCase(OutboundEventRequest request, DiagnosticReport report)
    {
        // TODO(WS-7): map request and report.Id into a SimulatedCase with an empty ResourceIds map.
        throw new NotImplementedException("WS7: ScreeningResultBuilder.CreateCase");
    }
}
