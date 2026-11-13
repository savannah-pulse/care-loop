using CareLoop.Domain.Cases;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace CareLoop.Api.Features.Fhir.Outbound;

/// <summary>
/// Builds the <c>Task</c> that tells the HIS about an escalation or a call request.
/// </summary>
/// <remarks>
/// Addresses the task to the escalated role or the current owner, references the case <c>Task</c> as its focus and
/// carries no patient detail: the HIS finds the patient through the case task. The logical identifier is derived from
/// the domain event identifier, so a retried delivery overwrites the same notice instead of creating a second one.
/// </remarks>
public sealed class EscalationTaskMapper
{
    /// <summary>
    /// Builds the FHIR resource.
    /// </summary>
    /// <param name="eventId">The domain event the notice is for; makes the task identifier.</param>
    /// <param name="caseId">The case the notice is about.</param>
    /// <param name="addressedTo">The role, queue or owner the notice is addressed to.</param>
    /// <param name="taskCode"><see cref="StatusMap.EscalationTaskCode"/> or <see cref="StatusMap.CallRequestTaskCode"/>.</param>
    /// <param name="reason">Why the notice is raised, for example the escalation level and reason; never patient detail.</param>
    /// <param name="raisedAt">When the notice was raised.</param>
    /// <returns>A <c>Task</c> ready to send to the HIS.</returns>
    public FhirTask Map(Guid eventId, Guid caseId, CaseOwner addressedTo, string taskCode, string? reason, DateTimeOffset raisedAt)
    {
        ArgumentNullException.ThrowIfNull(addressedTo);

        return new FhirTask
        {
            Id = $"notice-{eventId:N}",
            Status = FhirTask.TaskStatus.Requested,
            Intent = FhirTask.TaskIntent.Order,
            Code = StatusMap.TaskCode(taskCode),
            Focus = new ResourceReference($"Task/{OutboundSupport.CaseTaskId(caseId)}"),
            Owner = OutboundSupport.Reference(addressedTo),
            AuthoredOnElement = new FhirDateTime(raisedAt),
            LastModifiedElement = new FhirDateTime(raisedAt),
            Description = reason,
        };
    }
}
