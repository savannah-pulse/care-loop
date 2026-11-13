using CareLoop.Domain.Cases;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Recipients;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace CareLoop.Api.Features.Fhir.Outbound;

/// <summary>
/// Builds the case <c>Task</c> that represents a case's status to external systems and the patient view.
/// </summary>
/// <remarks>
/// Will apply the documented field mapping: handoff status to <c>Task.status</c>; stage to <c>Task.businessStatus</c>; owner to <c>Task.owner</c>; who handed over to <c>Task.requester</c>; the result or referral to <c>Task.focus</c>; patient to <c>Task.for</c>; deadline to <c>Task.restriction.period</c>; rejection or closure reason to <c>Task.statusReason</c>. All codes come from <see cref="StatusMap"/>.
/// </remarks>
public sealed class CaseTaskMapper
{
    /// <summary>
    /// Builds the FHIR resource.
    /// </summary>
    /// <param name="case">The case to represent.</param>
    /// <returns>The case <c>Task</c>, with the case identifier as its logical identifier.</returns>
    public FhirTask Map(Case @case)
    {
        // TODO(WS-3): apply the Case to Task field mapping using StatusMap.
        throw new NotImplementedException("WS3: CaseTaskMapper.Map");
    }
}
