using CareLoop.Contracts.Events;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>Appointment</c> to an appointment update.
/// </summary>
/// <remarks>
/// Will translate <c>booked</c> to scheduled, <c>fulfilled</c> to attended and <c>noshow</c> to missed through <see cref="StatusMap"/>; other statuses produce no event.
/// </remarks>
public sealed class AppointmentMapper : IFhirInboundMapper
{
    /// <inheritdoc/>
    public string ResourceType => "Appointment";

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        // TODO(WS-3): map the Appointment status to an AppointmentUpdatePayload.
        throw new NotImplementedException("WS3: AppointmentMapper.MapAsync");
    }
}
