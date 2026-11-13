using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>Procedure</c> to an outcome event.
/// </summary>
/// <remarks>
/// Produces an <see cref="OutcomePayload"/> whose evidence reference is the procedure reference and whose evidence
/// kind (C21) is <c>Procedure.code</c> in <see cref="StatusMap.EvidenceKindSystem"/>. Only a completed procedure counts
/// as outcome evidence. The procedure names the screening result in <c>reasonReference</c>.
/// </remarks>
public sealed class ProcedureMapper : IFhirInboundMapper
{
    /// <inheritdoc/>
    public string ResourceType => "Procedure";

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        var procedure = (Procedure)resource;

        if (procedure.Status != EventStatus.Completed)
        {
            IReadOnlyList<EventEnvelope> none = [];
            return System.Threading.Tasks.Task.FromResult(none);
        }

        var kind = StatusMap.ToEvidenceKind(procedure.Code)
            ?? throw new FormatException($"The Procedure carries no evidence kind (code in {StatusMap.EvidenceKindSystem}).");

        var performed = procedure.Performed switch
        {
            FhirDateTime at => InboundSupport.Instant(at),
            Period period => InboundSupport.Instant(period.EndElement) ?? InboundSupport.Instant(period.StartElement),
            _ => null,
        };

        IReadOnlyList<EventEnvelope> result =
        [
            new EventEnvelope
            {
                SourceSystem = sourceSystem,
                SourceEventId = InboundSupport.SourceEventId(procedure),
                Type = CaseEventType.OutcomeRecorded,
                SourceResultId = InboundSupport.RequireResultId(procedure, procedure.ReasonReference),
                OccurredAt = InboundSupport.OccurredAt(procedure, performed),
                Actor = procedure.Performer.Select(p => InboundSupport.ReadActor(p.Actor)).FirstOrDefault(a => a is not null),
                Payload = new OutcomePayload
                {
                    EvidenceReference = InboundSupport.ReferenceTo(procedure),
                    EvidenceKind = kind,
                },
            },
        ];
        return System.Threading.Tasks.Task.FromResult(result);
    }
}
