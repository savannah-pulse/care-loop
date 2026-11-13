using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Fhir.Inbound;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Tracks;
using Hl7.Fhir.Model;
using static CareLoop.Tests.Api.Fhir.FhirTestSupport;
using FhirTask = Hl7.Fhir.Model.Task;
using FhirTaskStatus = Hl7.Fhir.Model.Task.TaskStatus;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// Maps Samples/Task.json (a completed review task) and its consultation, referral and case variants.
/// </summary>
public sealed class TaskMapperTests
{
    private static readonly TaskMapper Mapper = new();

    [Fact]
    public void Completed_review_task_maps_to_a_clinical_review_with_priority_as_urgency()
    {
        var envelope = Assert.Single(Map(Mapper, Sample<FhirTask>("Task")));

        Assert.Equal(CaseEventType.ClinicalReviewCompleted, envelope.Type);
        Assert.Equal("SYN-RESULT-0001", envelope.SourceResultId);
        var payload = Assert.IsType<ClinicalReviewPayload>(envelope.Payload);
        Assert.Equal("PLACEHOLDER-URGENCY-A", payload.UrgencyCode);
        Assert.Equal("Task/SYN-TASK-0001", payload.ReviewTaskReference);
        Assert.Equal("SYN-STAFF-0001", envelope.Actor?.Reference);
        Assert.Equal("clinician", envelope.Actor?.Role);

        var withPriority = Sample<FhirTask>("Task");
        withPriority.RemoveExtension(StatusMap.ReviewedUrgencyExtension);
        withPriority.Priority = RequestPriority.Urgent;
        Assert.Equal("urgent", ((ClinicalReviewPayload)Map(Mapper, withPriority)[0].Payload).UrgencyCode);
    }

    [Fact]
    public void Review_task_without_urgency_throws_FormatException()
    {
        var task = Sample<FhirTask>("Task");
        task.RemoveExtension(StatusMap.ReviewedUrgencyExtension);

        Assert.Throws<FormatException>(() => Map(Mapper, task));
    }

    [Theory]
    [InlineData(FhirTaskStatus.Requested, ConsultationStatus.Requested)]
    [InlineData(FhirTaskStatus.Accepted, ConsultationStatus.Accepted)]
    [InlineData(FhirTaskStatus.Rejected, ConsultationStatus.Rejected)]
    [InlineData(FhirTaskStatus.Completed, ConsultationStatus.OutcomeRecorded)]
    public void Consultation_task_statuses_map_to_consultation_updates(FhirTaskStatus status, ConsultationStatus expected)
    {
        var task = TrackTask(StatusMap.ConsultationTaskCode, status);
        if (status != FhirTaskStatus.Requested)
        {
            task.Owner = new ResourceReference("Practitioner/SYN-STAFF-0002").WithRole("receiving-clinician");
        }

        var envelope = Assert.Single(Map(Mapper, task));

        Assert.Equal(CaseEventType.ConsultationUpdated, envelope.Type);
        var payload = Assert.IsType<ConsultationUpdatePayload>(envelope.Payload);
        Assert.Equal(expected, payload.Status);
        Assert.Equal("ServiceRequest/SYN-SR-0001", payload.ServiceRequestReference);
        Assert.Equal("Task/SYN-TASK-0002", payload.TaskReference);
    }

    [Fact]
    public void Requested_consultation_task_with_an_owner_proposes_that_owner()
    {
        var task = TrackTask(StatusMap.ConsultationTaskCode, FhirTaskStatus.Requested);
        task.Owner = new ResourceReference("Practitioner/SYN-STAFF-0002").WithRole("receiving-clinician", "SYN-FAC-B");
        task.Requester = new ResourceReference("Practitioner/SYN-STAFF-0001").WithRole("clinician");

        var envelope = Assert.Single(Map(Mapper, task));

        var payload = Assert.IsType<ConsultationUpdatePayload>(envelope.Payload);
        Assert.Equal(ConsultationStatus.OwnerProposed, payload.Status);
        Assert.Equal(
            new CaseOwner { Reference = "SYN-STAFF-0002", Role = "receiving-clinician", FacilityId = "SYN-FAC-B" },
            payload.ProposedOwner);
        Assert.Equal("SYN-STAFF-0001", envelope.Actor?.Reference);
    }

    [Fact]
    public void Referral_task_rejected_maps_to_a_referral_rejected_update()
    {
        var task = TrackTask(StatusMap.ReferralTaskCode, FhirTaskStatus.Rejected);
        task.Owner = new ResourceReference("Organization/SYN-FAC-B");
        task.StatusReason = new CodeableConcept { Text = "PLACEHOLDER - no capacity" };

        var envelope = Assert.Single(Map(Mapper, task));

        Assert.Equal(CaseEventType.ReferralUpdated, envelope.Type);
        var payload = Assert.IsType<ReferralUpdatePayload>(envelope.Payload);
        Assert.Equal(ReferralStatus.Rejected, payload.Status);
        Assert.Equal("PLACEHOLDER - no capacity", payload.Reason);
        Assert.Equal("SYN-FAC-B", payload.ReceivingFacilityId);
    }

    [Fact]
    public void Completed_referral_task_is_not_counted_as_care()
    {
        Assert.Empty(Map(Mapper, TrackTask(StatusMap.ReferralTaskCode, FhirTaskStatus.Completed)));
    }

    [Fact]
    public void Completed_case_task_maps_to_a_closure_decision()
    {
        var provenance = Sample<Provenance>("Provenance");
        var caseTask = (FhirTask)provenance.Contained[0];
        provenance.Contained.Clear();
        provenance.Target = [new ResourceReference("Task/SYN-CASE-TASK-0001")];
        caseTask.Contained.Add(provenance);
        caseTask.RelevantHistory.Add(new ResourceReference("#SYN-PROV-0001"));
        caseTask.Meta = new Meta { VersionId = "3" };

        var envelope = Assert.Single(Map(Mapper, caseTask));

        Assert.Equal(CaseEventType.ClosureDecided, envelope.Type);
        Assert.Equal("Task/SYN-CASE-TASK-0001/_history/3", envelope.SourceEventId);
        Assert.Equal("SYN-RESULT-0001", envelope.SourceResultId);
        Assert.Equal("SYN-STAFF-0002", envelope.Actor?.Reference);
        var payload = Assert.IsType<ClosurePayload>(envelope.Payload);
        Assert.Equal(ClosureReason.Treated, payload.Reason);
        Assert.Equal("Procedure/SYN-PROC-0001", payload.EvidenceReference);
        Assert.Equal("Provenance/SYN-PROV-0001", payload.ProvenanceReference);
    }

    [Fact]
    public void Case_task_carries_surveillance_date_attempts_and_exception_note()
    {
        var caseTask = (FhirTask)Sample<Provenance>("Provenance").Contained[0];
        caseTask.StatusReason = StatusMap.ToStatusReason(ClosureReason.OnSurveillance);
        caseTask.Output.Add(Output(StatusMap.NextSurveillanceDateOutput, new Date("2027-01-01")));
        caseTask.Output.Add(Output(StatusMap.DocumentedAttemptOutput, new ResourceReference("Communication/SYN-COMM-0001")));
        caseTask.Output.Add(Output(StatusMap.DocumentedAttemptOutput, new ResourceReference("Communication/SYN-COMM-0002")));
        caseTask.Output.Add(Output(StatusMap.ExceptionNoteOutput, new FhirString("PLACEHOLDER note")));

        var payload = (ClosurePayload)Map(Mapper, caseTask)[0].Payload;

        Assert.Equal(ClosureReason.OnSurveillance, payload.Reason);
        Assert.Equal(new DateOnly(2027, 1, 1), payload.NextSurveillanceDate);
        Assert.Equal(["Communication/SYN-COMM-0001", "Communication/SYN-COMM-0002"], payload.DocumentedAttemptReferences);
        Assert.Equal("PLACEHOLDER note", payload.ExceptionNote);
    }

    [Fact]
    public void Case_task_without_provenance_has_no_authorizer()
    {
        var caseTask = (FhirTask)Sample<Provenance>("Provenance").Contained[0];

        Assert.Null(Assert.Single(Map(Mapper, caseTask)).Actor);
    }

    [Fact]
    public void Unrecognised_task_maps_to_no_event()
    {
        var foreign = Sample<FhirTask>("Task");
        foreign.Code = new CodeableConcept("https://example.invalid/other", "clinical-review");
        var openReview = Sample<FhirTask>("Task");
        openReview.Status = FhirTaskStatus.InProgress;

        Assert.Empty(Map(Mapper, foreign));
        Assert.Empty(Map(Mapper, openReview));
    }

    [Fact]
    public void Source_event_id_changes_when_the_task_version_changes()
    {
        var task = TrackTask(StatusMap.ConsultationTaskCode, FhirTaskStatus.Requested);
        task.Meta = new Meta { VersionId = "1" };
        var first = Map(Mapper, task)[0].SourceEventId;

        task.Meta.VersionId = "2";
        var second = Map(Mapper, task)[0].SourceEventId;

        Assert.Equal("Task/SYN-TASK-0002/_history/1", first);
        Assert.Equal("Task/SYN-TASK-0002/_history/2", second);
    }

    private static FhirTask TrackTask(string code, FhirTaskStatus status) => new()
    {
        Id = "SYN-TASK-0002",
        Status = status,
        Intent = FhirTask.TaskIntent.Order,
        Code = StatusMap.TaskCode(code),
        Focus = new ResourceReference("ServiceRequest/SYN-SR-0001"),
        BasedOn = [new ResourceReference("DiagnosticReport/SYN-RESULT-0001")],
        LastModifiedElement = new FhirDateTime("2026-01-02T09:00:00Z"),
    };

    private static FhirTask.OutputComponent Output(string type, DataType value) => new()
    {
        Type = new CodeableConcept(StatusMap.ClosureOutputSystem, type),
        Value = value,
    };
}
