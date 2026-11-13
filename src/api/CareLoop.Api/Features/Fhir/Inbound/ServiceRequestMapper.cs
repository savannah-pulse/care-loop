using CareLoop.Contracts.Events;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>ServiceRequest</c> to the start of a consultation or referral.
/// </summary>
/// <remarks>
/// Will read the request category to tell a consultation from a referral and produce the requested or sent update for the matching track. Later status changes arrive on the accompanying <c>Task</c> and are handled by <see cref="TaskMapper"/>.
/// </remarks>
public sealed class ServiceRequestMapper : IFhirInboundMapper
{
    /// <inheritdoc/>
    public string ResourceType => "ServiceRequest";

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        // TODO(WS-3): tell consultation from referral and build the initial track update.
        throw new NotImplementedException("WS3: ServiceRequestMapper.MapAsync");
    }
}
