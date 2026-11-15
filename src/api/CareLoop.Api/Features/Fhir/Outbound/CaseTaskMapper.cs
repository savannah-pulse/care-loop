using CareLoop.Domain.Cases;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Tracks;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace CareLoop.Api.Features.Fhir.Outbound;

/// <summary>
/// Builds the case <c>Task</c> that represents a case's status to external systems and the patient view.
/// </summary>
/// <remarks>
/// Applies the documented field mapping: handoff status to <c>Task.status</c>; stage to <c>Task.businessStatus</c>;
/// owner to <c>Task.owner</c>; who handed over to <c>Task.requester</c>; the result or referral to <c>Task.focus</c>;
/// patient to <c>Task.for</c>; deadline to <c>Task.restriction.period</c>; rejection or closure reason to
/// <c>Task.statusReason</c>. All codes come from <see cref="StatusMap"/>. The task always names the screening result in
/// <c>basedOn</c>, so a system that sends it back is understood by the inbound mappers. It carries references only: no
/// patient name, no clinical detail.
/// </remarks>
public sealed class CaseTaskMapper
{
    /// <summary>
    /// Builds the FHIR resource.
    /// </summary>
    /// <param name="case">The case to represent.</param>
    /// <param name="rejection">
    /// The handoff rejection being published, if any. Rejecting a handoff clears the proposal from the case, so the
    /// status <c>rejected</c> and the reason come from the event. When a newer handoff is already pending (a late
    /// delivery), the newer state wins and the rejection is not shown.
    /// </param>
    /// <returns>The case <c>Task</c>, with the case identifier as its logical identifier and the case version as its version.</returns>
    public FhirTask Map(Case @case, HandoffRejected? rejection = null)
    {
        ArgumentNullException.ThrowIfNull(@case);

        var handoffRejected = rejection is not null && @case.PendingAssignment is null && !@case.IsClosed;
        var result = new ResourceReference($"DiagnosticReport/{@case.SourceResultId}");
        var task = new FhirTask
        {
            Id = OutboundSupport.CaseTaskId(@case.Id),
            Meta = new Meta { VersionId = @case.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            Status = StatusMap.ToTaskStatus(@case, handoffRejected),
            Intent = FhirTask.TaskIntent.Order,
            Code = StatusMap.TaskCode(StatusMap.CaseTaskCode),
            BusinessStatus = StatusMap.ToBusinessStatus(@case.Stage),
            BasedOn = [result],
            Focus = @case.Referral?.SourceReference is { Length: > 0 } referral ? new ResourceReference(referral) : result,
            For = new ResourceReference(OutboundSupport.Typed("Patient", @case.PatientReference)),
            Owner = OutboundSupport.Reference(@case.Owner),
            Requester = @case.PendingAssignment is { } assignment ? OutboundSupport.Reference(assignment.ProposedBy) : null,
            AuthoredOnElement = new FhirDateTime(@case.CreatedAt),
            LastModifiedElement = new FhirDateTime(@case.UpdatedAt),
            StatusReason = handoffRejected ? StatusMap.ToRejectionReason(rejection!.Reason) : StatusReason(@case),
            Description = @case.NextAction,
        };

        if (@case.DueAt is { } dueAt && @case.Stage != CaseStage.Closed)
        {
            task.Restriction = new FhirTask.RestrictionComponent { Period = new Period { EndElement = new FhirDateTime(dueAt) } };
        }

        if (@case.Closure?.EvidenceReference is { Length: > 0 } evidence)
        {
            task.Output.Add(new FhirTask.OutputComponent
            {
                Type = new CodeableConcept(StatusMap.ClosureOutputSystem, StatusMap.EvidenceOutput),
                Value = new ResourceReference(evidence),
            });
        }

        return task;
    }

    private static CodeableConcept? StatusReason(Case @case)
    {
        if (@case.Closure is { } closure)
        {
            return StatusMap.ToStatusReason(closure.Reason);
        }

        return @case.Referral is { Status: ReferralStatus.Rejected } referral
            ? StatusMap.ToRejectionReason(referral.RejectionReason ?? "No reason given.")
            : null;
    }
}
