using CareLoop.Contracts.Events;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>Provenance</c> to the authorizer of a closure decision.
/// </summary>
/// <remarks>
/// Will read the agent who authorized the target case <c>Task</c> and supply it as the actor of the closure event. A closure without a provenance agent has no authorizer and must be refused downstream, because closure needs a human.
/// </remarks>
public sealed class ProvenanceMapper : IFhirInboundMapper
{
    /// <inheritdoc/>
    public string ResourceType => "Provenance";

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        // TODO(WS-3): extract the authorizing agent and attach it to the closure envelope for the target Task.
        throw new NotImplementedException("WS3: ProvenanceMapper.MapAsync");
    }
}
