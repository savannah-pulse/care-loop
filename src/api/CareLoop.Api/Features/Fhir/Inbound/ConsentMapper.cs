using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using Hl7.Fhir.Model;
using DomainContactPoint = CareLoop.Domain.Recipients.ContactPoint;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>Consent</c> to a caregiver consent grant or withdrawal (C13).
/// </summary>
/// <remarks>
/// <c>status</c> <c>active</c> is a grant, <c>inactive</c> or <c>rejected</c> a withdrawal; other statuses produce no
/// event. An <c>active</c> consent whose <c>provision.type</c> is <c>deny</c> records the patient refusing, so it is a
/// withdrawal too: reading it as a grant would let CareLoop message a caregiver the patient said no to. Who recorded
/// the consent is the first <c>performer</c> carrying a CareLoop role (staff acting for the patient), so the audit
/// names them; a consent the patient performed alone names no actor. The caregiver is the <c>provision.actor</c>, a contained <c>RelatedPerson</c> whose first usable
/// <c>telecom</c> is the contact point. Only that contact point and the consent reference cross the boundary, never
/// the caregiver's name or the consent text. The consent names the screening result through the
/// <see cref="StatusMap.ScreeningResultExtension"/>, because a <c>Consent</c> has no standard field for it.
/// </remarks>
public sealed class ConsentMapper : IFhirInboundMapper
{
    /// <inheritdoc/>
    public string ResourceType => "Consent";

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        var consent = (Consent)resource;

        ConsentUpdateKind? kind = consent.Status switch
        {
            Consent.ConsentState.Active when consent.Provision?.Type == Consent.ConsentProvisionType.Deny => ConsentUpdateKind.Withdrawn,
            Consent.ConsentState.Active => ConsentUpdateKind.Granted,
            Consent.ConsentState.Inactive or Consent.ConsentState.Rejected => ConsentUpdateKind.Withdrawn,
            _ => null,
        };

        if (kind is null)
        {
            IReadOnlyList<EventEnvelope> none = [];
            return System.Threading.Tasks.Task.FromResult(none);
        }

        IReadOnlyList<EventEnvelope> result =
        [
            new EventEnvelope
            {
                SourceSystem = sourceSystem,
                SourceEventId = InboundSupport.SourceEventId(consent),
                Type = CaseEventType.ConsentRecorded,
                SourceResultId = InboundSupport.RequireResultId(consent),
                OccurredAt = InboundSupport.OccurredAt(consent, InboundSupport.Instant(consent.DateTimeElement)),
                Actor = consent.Performer.Select(InboundSupport.ReadActor).FirstOrDefault(a => a is not null),
                Payload = new ConsentPayload
                {
                    Kind = kind.Value,
                    ConsentReference = InboundSupport.ReferenceTo(consent),
                    CaregiverContactPoint = kind == ConsentUpdateKind.Granted ? Caregiver(consent) : null,
                },
            },
        ];
        return System.Threading.Tasks.Task.FromResult(result);
    }

    private static DomainContactPoint? Caregiver(Consent consent)
    {
        var caregivers = consent.Provision?.Actor
            .Select(a => InboundSupport.Contained<RelatedPerson>(consent, a.Reference))
            .Where(p => p is not null) ?? [];

        return caregivers
            .SelectMany(p => p!.Telecom)
            .Where(t => !string.IsNullOrWhiteSpace(t.Value))
            .Select(t => (Channel: StatusMap.ToNotificationChannel(t.System), t.Value))
            .Where(t => t.Channel is not null)
            .Select(t => new DomainContactPoint { Channel = t.Channel!.Value, Address = t.Value! })
            .FirstOrDefault();
    }
}
