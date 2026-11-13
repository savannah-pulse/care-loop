using CareLoop.Contracts.Events;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>Provenance</c> to the authorizer of a closure decision.
/// </summary>
/// <remarks>
/// The <c>Provenance</c> targets the case <c>Task</c>, which it contains (<c>target</c> = <c>#id</c>), so the one
/// resource carries both the decision and who made it. The agent who authorized it becomes the actor of the
/// closure event. A closure without a provenance agent (or without a role on it) has no authorizer and is refused
/// downstream, because closure needs a human. A <c>Provenance</c> about anything other than a case task is not a
/// closure and produces no event.
/// </remarks>
public sealed class ProvenanceMapper : IFhirInboundMapper
{
    /// <inheritdoc/>
    public string ResourceType => "Provenance";

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        var provenance = (Provenance)resource;
        IReadOnlyList<EventEnvelope> none = [];

        var taskTarget = provenance.Target.FirstOrDefault(t =>
            t.Reference?.StartsWith('#') == true
                ? InboundSupport.Contained<FhirTask>(provenance, t) is not null
                : InboundSupport.Parse(t.Reference) is { Type: "Task" });

        if (taskTarget is null)
        {
            return System.Threading.Tasks.Task.FromResult(none);
        }

        var caseTask = InboundSupport.Contained<FhirTask>(provenance, taskTarget)
            ?? throw new FormatException("A closure Provenance must contain the case Task it targets (target = #id).");

        if (StatusMap.CodeIn(caseTask.Code, StatusMap.TaskCodeSystem) != StatusMap.CaseTaskCode)
        {
            return System.Threading.Tasks.Task.FromResult(none);
        }

        var envelope = TaskMapper.MapClosure(caseTask, provenance, provenance, sourceSystem);
        IReadOnlyList<EventEnvelope> result = envelope is null ? none : [envelope];
        return System.Threading.Tasks.Task.FromResult(result);
    }
}
