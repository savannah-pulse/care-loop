using CareLoop.Contracts.Events;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>Procedure</c> to an outcome event.
/// </summary>
/// <remarks>
/// Will produce an <see cref="CareLoop.Contracts.Events.Payloads.OutcomePayload"/> whose evidence reference is the procedure reference. Only a completed procedure counts as outcome evidence.
/// </remarks>
public sealed class ProcedureMapper : IFhirInboundMapper
{
    /// <inheritdoc/>
    public string ResourceType => "Procedure";

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        // TODO(WS-3): build the OutcomePayload from a completed Procedure.
        throw new NotImplementedException("WS3: ProcedureMapper.MapAsync");
    }
}
