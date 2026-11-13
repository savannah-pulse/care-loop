using CareLoop.Contracts.Events;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>Task</c> to a review, consultation, referral or closure event.
/// </summary>
/// <remarks>
/// Will inspect the task code and focus to decide what the task is about: a completed review task becomes a clinical review with <c>Task.priority</c> as the reviewed urgency; a consultation or referral task becomes a status update using <see cref="StatusMap"/> (requested, accepted, rejected, completed); the case task set to completed becomes a closure decision, which also needs the <c>Provenance</c> naming the authorizer.
/// </remarks>
public sealed class TaskMapper : IFhirInboundMapper
{
    /// <inheritdoc/>
    public string ResourceType => "Task";

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        // TODO(WS-3): classify the Task and translate its status through StatusMap into one envelope.
        throw new NotImplementedException("WS3: TaskMapper.MapAsync");
    }
}
