using CareLoop.Domain.Cases;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Recipients;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace CareLoop.Api.Features.Fhir.Outbound;

/// <summary>
/// Builds the <c>Task</c> that tells the HIS about an escalation or a call request.
/// </summary>
/// <remarks>
/// Will address the task to the escalated role or the current owner, reference the case <c>Task</c> as its focus and carry no patient detail beyond the patient reference.
/// </remarks>
public sealed class EscalationTaskMapper
{
    /// <summary>
    /// Builds the FHIR resource.
    /// </summary>
    /// <param name="caseId">The case the notice is about.</param>
    /// <param name="addressedTo">The role, queue or owner the notice is addressed to.</param>
    /// <param name="reasonCode">Why the notice is raised, for example an escalation level or a call request.</param>
    /// <param name="raisedAt">When the notice was raised.</param>
    /// <returns>A <c>Task</c> ready to send to the HIS.</returns>
    public FhirTask Map(Guid caseId, CaseOwner addressedTo, string reasonCode, DateTimeOffset raisedAt)
    {
        // TODO(WS-3): populate owner, focus, code and authoredOn.
        throw new NotImplementedException("WS3: EscalationTaskMapper.Map");
    }
}
