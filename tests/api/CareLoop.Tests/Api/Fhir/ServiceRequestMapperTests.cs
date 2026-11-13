using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Fhir.Inbound;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using CareLoop.Domain.Tracks;
using Hl7.Fhir.Model;
using static CareLoop.Tests.Api.Fhir.FhirTestSupport;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// Maps Samples/ServiceRequest.json (a consultation request) and its referral variant.
/// </summary>
public sealed class ServiceRequestMapperTests
{
    private static readonly ServiceRequestMapper Mapper = new();

    [Fact]
    public void Consultation_request_maps_to_consultation_requested()
    {
        var envelope = Assert.Single(Map(Mapper, Sample<ServiceRequest>("ServiceRequest")));

        Assert.Equal(CaseEventType.ConsultationUpdated, envelope.Type);
        Assert.Equal("SYN-RESULT-0001", envelope.SourceResultId);
        Assert.Equal("ServiceRequest/SYN-SR-0001/_history/1", envelope.SourceEventId);
        Assert.Equal("clinician", envelope.Actor?.Role);
        var payload = Assert.IsType<ConsultationUpdatePayload>(envelope.Payload);
        Assert.Equal(ConsultationStatus.Requested, payload.Status);
        Assert.Equal("ServiceRequest/SYN-SR-0001", payload.ServiceRequestReference);
    }

    [Fact]
    public void Referral_request_maps_to_referral_sent()
    {
        var request = Sample<ServiceRequest>("ServiceRequest");
        request.Category = [new CodeableConcept(StatusMap.ServiceRequestCategorySystem, StatusMap.ReferralTaskCode)];
        request.Performer = [new ResourceReference("Organization/SYN-FAC-B")];

        var envelope = Assert.Single(Map(Mapper, request));

        Assert.Equal(CaseEventType.ReferralUpdated, envelope.Type);
        var payload = Assert.IsType<ReferralUpdatePayload>(envelope.Payload);
        Assert.Equal(ReferralStatus.Sent, payload.Status);
        Assert.Equal("SYN-FAC-B", payload.ReceivingFacilityId);
    }

    [Fact]
    public void Request_with_unknown_category_maps_to_no_event()
    {
        var unknown = Sample<ServiceRequest>("ServiceRequest");
        unknown.Category = [new CodeableConcept { Text = "PLACEHOLDER" }];
        var revoked = Sample<ServiceRequest>("ServiceRequest");
        revoked.Status = RequestStatus.Revoked;

        Assert.Empty(Map(Mapper, unknown));
        Assert.Empty(Map(Mapper, revoked));
    }

    [Fact]
    public void Request_without_a_screening_result_throws_FormatException()
    {
        var request = Sample<ServiceRequest>("ServiceRequest");
        request.ReasonReference.Clear();

        Assert.Throws<FormatException>(() => Map(Mapper, request));
    }
}
