using CareLoop.Api.Features.Fhir.Inbound;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using Hl7.Fhir.Model;
using static CareLoop.Tests.Api.Fhir.FhirTestSupport;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// Maps Samples/Consent.json: caregiver consent (C13).
/// </summary>
public sealed class ConsentMapperTests
{
    private static readonly ConsentMapper Mapper = new();

    [Fact]
    public void Active_consent_maps_to_a_grant_with_the_caregiver_contact_point()
    {
        var envelope = Assert.Single(Map(Mapper, Sample<Consent>("Consent")));

        Assert.Equal(CaseEventType.ConsentRecorded, envelope.Type);
        Assert.Equal("SYN-RESULT-0001", envelope.SourceResultId);
        Assert.Equal("Consent/SYN-CONSENT-0001/_history/1", envelope.SourceEventId);
        var payload = Assert.IsType<ConsentPayload>(envelope.Payload);
        Assert.Equal(ConsentUpdateKind.Granted, payload.Kind);
        Assert.Equal("Consent/SYN-CONSENT-0001", payload.ConsentReference);
        Assert.Equal(new CareLoop.Domain.Recipients.ContactPoint { Channel = NotificationChannel.Email, Address = "SYN-CG-INBOX-0001" }, payload.CaregiverContactPoint);
    }

    [Theory]
    [InlineData(Consent.ConsentState.Inactive)]
    [InlineData(Consent.ConsentState.Rejected)]
    public void Inactive_or_rejected_consent_maps_to_a_withdrawal(Consent.ConsentState status)
    {
        var consent = Sample<Consent>("Consent");
        consent.Status = status;
        consent.Meta!.VersionId = "2";

        var payload = Assert.IsType<ConsentPayload>(Assert.Single(Map(Mapper, consent)).Payload);

        Assert.Equal(ConsentUpdateKind.Withdrawn, payload.Kind);
        Assert.Null(payload.CaregiverContactPoint);
    }

    [Theory]
    [InlineData(Consent.ConsentState.Draft)]
    [InlineData(Consent.ConsentState.Proposed)]
    [InlineData(Consent.ConsentState.EnteredInError)]
    public void Other_statuses_map_to_no_event(Consent.ConsentState status)
    {
        var consent = Sample<Consent>("Consent");
        consent.Status = status;

        Assert.Empty(Map(Mapper, consent));
    }

    [Fact]
    public void Grant_without_a_caregiver_carries_no_contact_point()
    {
        var consent = Sample<Consent>("Consent");
        consent.Contained.Clear();
        consent.Provision = null;

        var payload = (ConsentPayload)Map(Mapper, consent)[0].Payload;

        Assert.Null(payload.CaregiverContactPoint);
    }
}
