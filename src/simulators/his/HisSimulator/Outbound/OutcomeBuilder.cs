using HisSimulator.State;
using Hl7.Fhir.Model;

namespace HisSimulator.Outbound;

/// <summary>
/// Builds the resource that records a follow-up outcome: a <c>Procedure</c> or a follow-up <c>DiagnosticReport</c>.
/// </summary>
/// <remarks>
/// The outcome resource becomes the evidence reference CareLoop requires before closure, so its
/// logical id must be stable and is stored on the simulated case for the closure event to cite.
/// The kind of evidence is the placeholder code in <c>Params["evidenceKind"]</c>; the simulator does not
/// state a clinical finding.
/// </remarks>
public sealed class OutcomeBuilder
{
    /// <summary>
    /// Builds a completed <c>Procedure</c> recording the outcome.
    /// </summary>
    /// <param name="case">The simulated case; supplies the patient and the referral or report the procedure is based on.</param>
    /// <param name="request">The event to represent; the <c>evidenceKind</c> param is required.</param>
    /// <returns>
    /// A completed <c>Procedure</c> for the synthetic patient with the placeholder evidence kind as its code,
    /// a deterministic id and a simulated-data tag.
    /// </returns>
    /// <exception cref="ArgumentException">The evidence kind is missing.</exception>
    public Procedure BuildProcedure(SimulatedCase @case, OutboundEventRequest request)
    {
        // TODO(WS-7): build the outcome Procedure.
        throw new NotImplementedException("WS7: OutcomeBuilder.BuildProcedure");
    }

    /// <summary>
    /// Builds a follow-up <c>DiagnosticReport</c> recording the outcome.
    /// </summary>
    /// <param name="case">The simulated case; supplies the patient and the original screening report.</param>
    /// <param name="request">The event to represent; the <c>evidenceKind</c> param is required.</param>
    /// <returns>
    /// A final <c>DiagnosticReport</c> that is distinguishable from a new screening result (so it does not open
    /// a second case), references the original result, and carries a deterministic id and a simulated-data tag.
    /// </returns>
    /// <exception cref="ArgumentException">The evidence kind is missing.</exception>
    public DiagnosticReport BuildFollowUpReport(SimulatedCase @case, OutboundEventRequest request)
    {
        // TODO(WS-7): build the follow-up DiagnosticReport; agree with WS6 how it is told apart from a screening result.
        throw new NotImplementedException("WS7: OutcomeBuilder.BuildFollowUpReport");
    }
}
