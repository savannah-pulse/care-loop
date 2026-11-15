using System.Net;
using System.Text;
using System.Text.Json;
using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Fhir.Inbound;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using Hl7.Fhir.Model;
using static CareLoop.Tests.Api.Fhir.FhirTestSupport;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// Maps Samples/DiagnosticReport.json (screening result, with a contained Patient) and a follow-up report.
/// </summary>
public sealed class DiagnosticReportMapperTests
{
    private static readonly DiagnosticReportMapper Mapper = new(UnconfiguredClient());

    [Fact]
    public void New_report_maps_to_a_screening_result_event()
    {
        var envelope = Assert.Single(Map(Mapper, Sample<DiagnosticReport>("DiagnosticReport")));

        Assert.Equal(CaseEventType.ScreeningResultReceived, envelope.Type);
        Assert.Equal(SourceSystemId, envelope.SourceSystem);
        Assert.Equal("SYN-RESULT-0001", envelope.SourceResultId);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero), envelope.OccurredAt);

        var payload = Assert.IsType<ScreeningResultPayload>(envelope.Payload);
        Assert.Equal("SYN-PT-0001", payload.PatientReference);
        Assert.Equal("breast-screening", payload.PathwayId);
        Assert.Equal("SYN-FAC-A", payload.FacilityId);
        Assert.Equal("DiagnosticReport/SYN-RESULT-0001", payload.ResultReference);
        Assert.Equal("en", payload.PatientLanguage);
        var contact = Assert.Single(payload.PatientContactPoints);
        Assert.Equal(new CareLoop.Domain.Recipients.ContactPoint { Channel = NotificationChannel.Simulator, Address = "SYN-INBOX-0001" }, contact);
        Assert.Equal("SYN-STAFF-0001", payload.NamedOwner?.Reference);
        Assert.Equal("clinician", payload.NamedOwner?.Role);
    }

    [Fact]
    public void Contained_patient_supplies_display_name_and_mrn()
    {
        var payload = (ScreeningResultPayload)Map(Mapper, Sample<DiagnosticReport>("DiagnosticReport"))[0].Payload;

        Assert.Equal("SYNTHETIC Patient One", payload.PatientDisplayName);
        Assert.Equal("SYN-MRN-0001", payload.PatientSecondaryIdentifier);
    }

    [Fact]
    public void Referenced_patient_is_read_from_the_source_system()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleJson("Patient"), Encoding.UTF8, FhirEndpoints.FhirJson),
        });
        var report = Sample<DiagnosticReport>("DiagnosticReport");
        report.Contained.Clear();
        report.Subject = new ResourceReference("Patient/SYN-PT-0001");

        var payload = (ScreeningResultPayload)Map(new DiagnosticReportMapper(Client(handler)), report)[0].Payload;

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://his.invalid/fhir/Patient/SYN-PT-0001", request.Uri.ToString());
        Assert.Equal("SYN-PT-0001", payload.PatientReference);
        Assert.Equal("SYNTHETIC Patient-One", payload.PatientDisplayName);
        Assert.Equal("SYN-MRN-0001", payload.PatientSecondaryIdentifier);
    }

    [Fact]
    public void Patient_that_cannot_be_read_leaves_the_demographics_empty()
    {
        var report = Sample<DiagnosticReport>("DiagnosticReport");
        report.Contained.Clear();
        report.Subject = new ResourceReference("Patient/SYN-PT-0001");

        var payload = (ScreeningResultPayload)Map(Mapper, report)[0].Payload;

        Assert.Equal("SYN-PT-0001", payload.PatientReference);
        Assert.Null(payload.PatientDisplayName);
        Assert.Null(payload.PatientSecondaryIdentifier);
        Assert.Empty(payload.PatientContactPoints);
    }

    [Fact]
    public void Follow_up_report_maps_to_an_outcome_event()
    {
        var report = FollowUpReport();

        var envelope = Assert.Single(Map(Mapper, report));

        Assert.Equal(CaseEventType.OutcomeRecorded, envelope.Type);
        Assert.Equal("SYN-RESULT-0001", envelope.SourceResultId);
        var payload = Assert.IsType<OutcomePayload>(envelope.Payload);
        Assert.Equal("DiagnosticReport/SYN-FOLLOWUP-0001", payload.EvidenceReference);
        Assert.Equal("diagnosis", payload.EvidenceKind);
        Assert.Equal("clinician", envelope.Actor?.Role);
    }

    [Fact]
    public void Follow_up_report_without_an_evidence_kind_throws_FormatException()
    {
        var report = FollowUpReport();
        report.Code = new CodeableConcept { Text = "PLACEHOLDER" };

        Assert.Throws<FormatException>(() => Map(Mapper, report));
    }

    [Fact]
    public void Source_event_id_is_resource_id_plus_version()
    {
        var report = Sample<DiagnosticReport>("DiagnosticReport");

        Assert.Equal("DiagnosticReport/SYN-RESULT-0001/_history/1", Map(Mapper, report)[0].SourceEventId);

        report.Meta!.VersionId = "2";
        Assert.Equal("DiagnosticReport/SYN-RESULT-0001/_history/2", Map(Mapper, report)[0].SourceEventId);
    }

    [Fact]
    public void Without_a_version_the_same_content_gives_the_same_source_event_id()
    {
        var first = Sample<DiagnosticReport>("DiagnosticReport");
        first.Meta!.VersionId = null;
        var resend = Sample<DiagnosticReport>("DiagnosticReport");
        resend.Meta!.VersionId = null;
        var changed = Sample<DiagnosticReport>("DiagnosticReport");
        changed.Meta!.VersionId = null;
        changed.Status = DiagnosticReport.DiagnosticReportStatus.Amended;

        var id = Map(Mapper, first)[0].SourceEventId;

        Assert.Equal(id, Map(Mapper, resend)[0].SourceEventId);
        Assert.NotEqual(id, Map(Mapper, changed)[0].SourceEventId);
    }

    [Fact]
    public void Report_without_a_subject_throws_FormatException()
    {
        var report = Sample<DiagnosticReport>("DiagnosticReport");
        report.Subject = null;

        Assert.Throws<FormatException>(() => Map(Mapper, report));
    }

    [Fact]
    public void Report_without_a_pathway_or_facility_throws_FormatException()
    {
        var noPathway = Sample<DiagnosticReport>("DiagnosticReport");
        noPathway.Category.Clear();
        var noFacility = Sample<DiagnosticReport>("DiagnosticReport");
        noFacility.Performer.Clear();

        Assert.Throws<FormatException>(() => Map(Mapper, noPathway));
        Assert.Throws<FormatException>(() => Map(Mapper, noFacility));
    }

    [Fact]
    public void Preliminary_report_maps_to_no_event()
    {
        var report = Sample<DiagnosticReport>("DiagnosticReport");
        report.Status = DiagnosticReport.DiagnosticReportStatus.Preliminary;

        Assert.Empty(Map(Mapper, report));
    }

    [Fact]
    public void No_result_values_are_copied_into_the_envelope()
    {
        var envelope = Map(Mapper, Sample<DiagnosticReport>("DiagnosticReport"))[0];

        var json = JsonSerializer.Serialize(envelope) + JsonSerializer.Serialize(envelope.Payload, envelope.Payload.GetType());

        Assert.DoesNotContain("PLACEHOLDER-RESULT-VALUE", json);
        Assert.DoesNotContain("Observation", json);
    }

    private static DiagnosticReport FollowUpReport()
    {
        var report = Sample<DiagnosticReport>("DiagnosticReport");
        report.Id = "SYN-FOLLOWUP-0001";
        report.Contained.Clear();
        report.Subject = new ResourceReference("Patient/SYN-PT-0001");
        report.Code = StatusMap.FromEvidenceKind("diagnosis");
        report.SetExtension(StatusMap.ScreeningResultExtension, new ResourceReference("DiagnosticReport/SYN-RESULT-0001"));
        return report;
    }
}
