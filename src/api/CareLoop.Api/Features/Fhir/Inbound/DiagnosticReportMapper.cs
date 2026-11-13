using CareLoop.Contracts.Events;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>DiagnosticReport</c> (with its <c>Observation</c>s) to a screening-result or outcome event.
/// </summary>
/// <remarks>
/// Will produce a <see cref="CareLoop.Contracts.Events.Payloads.ScreeningResultPayload"/> for a new screening report, which opens a case, and an <see cref="CareLoop.Contracts.Events.Payloads.OutcomePayload"/> for a follow-up report that refers to an existing result (it becomes the evidence reference). Only identifiers and references are carried over; result values stay in the HIS.
/// </remarks>
public sealed class DiagnosticReportMapper : IFhirInboundMapper
{
    /// <inheritdoc/>
    public string ResourceType => "DiagnosticReport";

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        // TODO(WS-3): decide new result versus follow-up report and build the matching envelope.
        throw new NotImplementedException("WS3: DiagnosticReportMapper.MapAsync");
    }
}
