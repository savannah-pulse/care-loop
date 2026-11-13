using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using CareLoop.Domain.Tracks;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>ServiceRequest</c> to the start of a consultation or referral.
/// </summary>
/// <remarks>
/// <c>ServiceRequest.category</c> in <see cref="StatusMap.ServiceRequestCategorySystem"/> tells a consultation from a
/// referral; an <c>active</c> request produces the requested or sent update for the matching track. The request
/// names the screening result in <c>reasonReference</c>. Later status changes arrive on the accompanying <c>Task</c>
/// and are handled by <see cref="TaskMapper"/>. Until that task exists, the request itself stands in as the task reference.
/// </remarks>
public sealed class ServiceRequestMapper : IFhirInboundMapper
{
    /// <inheritdoc/>
    public string ResourceType => "ServiceRequest";

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        var request = (ServiceRequest)resource;
        IReadOnlyList<EventEnvelope> none = [];

        if (request.Status != RequestStatus.Active)
        {
            return System.Threading.Tasks.Task.FromResult(none);
        }

        var category = request.Category
            .Select(c => StatusMap.CodeIn(c, StatusMap.ServiceRequestCategorySystem))
            .FirstOrDefault(c => c is not null);

        var reference = InboundSupport.ReferenceTo(request);
        EventPayload? payload = category switch
        {
            StatusMap.ConsultationTaskCode => new ConsultationUpdatePayload
            {
                Status = ConsultationStatus.Requested,
                ServiceRequestReference = reference,
                TaskReference = reference,
            },
            StatusMap.ReferralTaskCode => new ReferralUpdatePayload
            {
                Status = ReferralStatus.Sent,
                ServiceRequestReference = reference,
                TaskReference = reference,
                ReceivingFacilityId = request.Performer
                    .Select(p => InboundSupport.Parse(p.Reference))
                    .FirstOrDefault(p => p?.Type == "Organization")?.Id,
            },
            _ => null,
        };

        if (payload is null)
        {
            return System.Threading.Tasks.Task.FromResult(none);
        }

        IReadOnlyList<EventEnvelope> result =
        [
            new EventEnvelope
            {
                SourceSystem = sourceSystem,
                SourceEventId = InboundSupport.SourceEventId(request),
                Type = payload is ConsultationUpdatePayload ? CaseEventType.ConsultationUpdated : CaseEventType.ReferralUpdated,
                SourceResultId = InboundSupport.RequireResultId(request, request.ReasonReference),
                OccurredAt = InboundSupport.OccurredAt(request, InboundSupport.Instant(request.AuthoredOnElement)),
                Actor = InboundSupport.ReadActor(request.Requester),
                Payload = payload,
            },
        ];
        return System.Threading.Tasks.Task.FromResult(result);
    }
}
