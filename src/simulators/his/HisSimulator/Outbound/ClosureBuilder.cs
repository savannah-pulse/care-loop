using HisSimulator.State;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Outbound;

/// <summary>
/// Builds the closure decision: CareLoop's case <c>Task</c> set to completed, plus a <c>Provenance</c> naming the authorizer.
/// </summary>
/// <remarks>
/// Closure is where CareLoop stops watching a patient, so it needs an authorized human and evidence
/// or a documented exception. The builder sends exactly what the scenario or the clinician supplied,
/// including a closure with no evidence, so that CareLoop's refusal can be demonstrated. It never
/// fills in missing evidence.
/// </remarks>
public sealed class ClosureBuilder
{
    /// <summary>
    /// Builds the case <c>Task</c> in completed state.
    /// </summary>
    /// <param name="case">The simulated case; supplies CareLoop's case <c>Task</c> id and any outcome resource ids.</param>
    /// <param name="request">
    /// The event to represent; the <c>closureReason</c> param is required. Evidence comes from
    /// <c>Params["evidenceFromStep"]</c> (resolved to the outcome resource sent by that step), an explicit
    /// evidence reference, or a documented exception note; all are optional.
    /// </param>
    /// <returns>
    /// A <c>Task</c> with status completed, the typed closure reason in <c>statusReason</c>, the evidence reference
    /// in <c>output</c> when supplied, a deterministic version and a simulated-data tag.
    /// </returns>
    /// <exception cref="ArgumentException">The closure reason is missing.</exception>
    /// <exception cref="InvalidOperationException">CareLoop's case <c>Task</c> id is not known for the case.</exception>
    public FhirTask BuildTask(SimulatedCase @case, OutboundEventRequest request)
    {
        // TODO(WS-7): build the completed case Task; look up CareLoopTaskId (GET /fhir/Task) if not yet known.
        throw new NotImplementedException("WS7: ClosureBuilder.BuildTask");
    }

    /// <summary>
    /// Builds the <c>Provenance</c> that names who authorized the closure.
    /// </summary>
    /// <param name="case">The simulated case being closed.</param>
    /// <param name="request">The event to represent; the performer is the authorizer and is required.</param>
    /// <param name="closureTask">The completed case <c>Task</c> built by <see cref="BuildTask"/>, used as the provenance target.</param>
    /// <returns>
    /// A <c>Provenance</c> targeting the case <c>Task</c>, recorded at <see cref="OutboundEventRequest.OccurredAt"/>,
    /// with one agent (the synthetic authorizer and role), a deterministic id and a simulated-data tag.
    /// </returns>
    /// <exception cref="ArgumentException">The performer is missing.</exception>
    public Provenance BuildProvenance(SimulatedCase @case, OutboundEventRequest request, FhirTask closureTask)
    {
        // TODO(WS-7): build the Provenance naming the authorizer.
        throw new NotImplementedException("WS7: ClosureBuilder.BuildProvenance");
    }
}
