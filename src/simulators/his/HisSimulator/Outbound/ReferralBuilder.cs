using HisSimulator.State;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Outbound;

/// <summary>
/// Builds the <c>ServiceRequest</c> plus <c>Task</c> pair that represents a referral and its status.
/// </summary>
/// <remarks>
/// An accepted referral is still not completed care, and a rejected one must return the next action
/// to the current owner. The simulator therefore reports each referral state faithfully and never
/// collapses "accepted" into an outcome.
/// </remarks>
public sealed class ReferralBuilder
{
    /// <summary>
    /// Builds the referral <c>ServiceRequest</c>, sent once when the referral is first made.
    /// </summary>
    /// <param name="case">The simulated case; supplies the patient and the report the referral is based on.</param>
    /// <param name="request">The event to represent; the referring performer is required.</param>
    /// <returns>
    /// An active <c>ServiceRequest</c> categorised as a referral, with the synthetic patient as subject,
    /// the performer as requester, the receiving facility as intended performer, a deterministic id and a simulated-data tag.
    /// </returns>
    /// <exception cref="ArgumentException">The performer is missing.</exception>
    public ServiceRequest BuildServiceRequest(SimulatedCase @case, OutboundEventRequest request)
    {
        var resource = FhirSupport.Service(@case, request, "referral");
        var facility = FhirSupport.OptionalText(request, "receivingFacility");
        if (facility is not null)
        {
            if (!FhirSupport.SyntheticId(facility)) throw new ArgumentException("The receiving facility must be synthetic.");
            resource.Performer.Add(new ResourceReference("Organization/" + facility));
        }
        return resource;
    }

    /// <summary>
    /// Builds the referral <c>Task</c> in the state named by <see cref="OutboundEventRequest.Status"/>.
    /// </summary>
    /// <param name="case">The simulated case; supplies the id of the referral <c>ServiceRequest</c> for <c>Task.focus</c>.</param>
    /// <param name="request">The event to represent; status must be <c>requested</c>, <c>accepted</c> or <c>rejected</c>.</param>
    /// <returns>
    /// A <c>Task</c> with the mapped status, a stable logical id across the referral's states and a version that changes
    /// per state, the acting member of staff as owner, the rejection reason in <c>statusReason</c> when rejected
    /// (from <c>Params["reason"]</c>), and a simulated-data tag.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The status is missing or unsupported. <c>completed</c> is refused: a referral is never completed care, and CareLoop
    /// ignores a completed referral Task.
    /// </exception>
    public FhirTask BuildTask(SimulatedCase @case, OutboundEventRequest request)
    {
        return FhirSupport.TrackTask(@case, request, "referral");
    }
}
