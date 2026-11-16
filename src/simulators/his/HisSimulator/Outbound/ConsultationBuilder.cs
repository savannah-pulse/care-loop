using HisSimulator.State;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Outbound;

/// <summary>
/// Builds the <c>ServiceRequest</c> plus <c>Task</c> pair that represents a consultation and its status.
/// </summary>
/// <remarks>
/// A consultation request may propose a new owner (<c>Params["proposedOwner"]</c>, with a facility); without one it
/// requests the consultation only. Proposing is not
/// accepting: the <c>Task</c> stays <c>requested</c> until the proposed owner sends an accepted or
/// rejected update, which is how CareLoop's two-step ownership transfer is exercised.
/// </remarks>
public sealed class ConsultationBuilder
{
    /// <summary>
    /// Builds the consultation <c>ServiceRequest</c>, sent once when the consultation is first requested.
    /// </summary>
    /// <param name="case">The simulated case; supplies the patient and the report the request is based on.</param>
    /// <param name="request">The event to represent; the requesting performer is required.</param>
    /// <returns>
    /// An active <c>ServiceRequest</c> categorised as a consultation, with the synthetic patient as subject,
    /// the performer as requester, the screening report in <c>reasonReference</c>, a deterministic id and a simulated-data tag.
    /// </returns>
    /// <exception cref="ArgumentException">The performer is missing.</exception>
    public ServiceRequest BuildServiceRequest(SimulatedCase @case, OutboundEventRequest request)
    {
        return FhirSupport.Service(@case, request, "consultation");
    }

    /// <summary>
    /// Builds the consultation <c>Task</c> in the state named by <see cref="OutboundEventRequest.Status"/>.
    /// </summary>
    /// <param name="case">The simulated case; supplies the id of the consultation <c>ServiceRequest</c> for <c>Task.focus</c>.</param>
    /// <param name="request">The event to represent; status must be <c>requested</c>, <c>accepted</c>, <c>rejected</c> or <c>completed</c>.</param>
    /// <returns>
    /// A <c>Task</c> with the mapped status, the same logical id for every state of this consultation and a version
    /// that changes per state, <c>requester</c> the handing-over clinician, <c>owner</c> the proposed owner on a request
    /// (absent when none was proposed, so no handoff is implied) or the answering member of staff on an answer,
    /// a rejection reason in <c>statusReason</c> when rejected, and a simulated-data tag.
    /// </returns>
    /// <exception cref="ArgumentException">The status is missing or not one of the supported values.</exception>
    public FhirTask BuildTask(SimulatedCase @case, OutboundEventRequest request)
    {
        return FhirSupport.TrackTask(@case, request, "consultation");
    }
}
