using System.Text;
using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Fhir.Outbound;
using CareLoop.Api.Infrastructure.Persistence.Summaries;
using CareLoop.Contracts.Summaries;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Tests.Fakes;
using Hl7.Fhir.Model;
using FhirTaskStatus = Hl7.Fhir.Model.Task.TaskStatus;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// The outbound mappers: escalation and call-request notices, the handoff summary document and the case task.
/// </summary>
public sealed class OutboundMapperTests
{
    private static readonly DateTimeOffset At = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Escalation_maps_to_a_Task_addressed_to_the_escalated_role()
    {
        var eventId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        var task = new EscalationTaskMapper().Map(eventId, caseId, TestData.FacilityLeadQueue(), StatusMap.EscalationTaskCode, "Escalation level 1: overdue", At);

        Assert.Equal($"notice-{eventId:N}", task.Id);
        Assert.Equal(FhirTaskStatus.Requested, task.Status);
        Assert.Equal(StatusMap.EscalationTaskCode, StatusMap.CodeIn(task.Code, StatusMap.TaskCodeSystem));
        Assert.Equal($"Task/{caseId:D}", task.Focus!.Reference);
        Assert.Equal("SYN-QUEUE-FAC-A", task.Owner!.Identifier!.Value);
        Assert.Equal(StatusMap.QueueIdentifierSystem, task.Owner!.Identifier!.System);
        Assert.Equal("facility-lead", (task.Owner!.GetExtension(StatusMap.ActorRoleExtension)!.Value as Code)?.Value);
        Assert.Equal(At, new FhirDateTime(task.AuthoredOn).ToDateTimeOffset(TimeSpan.Zero));
        Assert.Null(task.For);
    }

    [Fact]
    public void Approved_summary_maps_to_a_DocumentReference()
    {
        var proposed = Proposed(Guid.NewGuid());
        var summary = Summary(proposed.CaseId, HandoffSummaryStatus.Approved);

        var document = new HandoffDocumentReferenceMapper().Map(proposed, summary);

        Assert.Equal($"handoff-{summary.Id:N}", document.Id);
        Assert.Equal(DocumentReferenceStatus.Current, document.Status);
        Assert.Equal("Practitioner/SYN-STAFF-0001", Assert.Single(document.Author).Reference);
        Assert.Equal($"Task/{proposed.CaseId:D}", Assert.Single(document.Context!.Related).Reference);
        var attachment = Assert.Single(document.Content).Attachment;
        Assert.Equal(summary.Text, Encoding.UTF8.GetString(attachment!.Data!));
    }

    [Theory]
    [InlineData(HandoffSummaryStatus.Draft)]
    [InlineData(HandoffSummaryStatus.Rejected)]
    public void Draft_summary_is_never_published(HandoffSummaryStatus status)
    {
        var proposed = Proposed(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => new HandoffDocumentReferenceMapper().Map(proposed, Summary(proposed.CaseId, status)));
    }

    [Fact]
    public async System.Threading.Tasks.Task Case_maps_to_a_Task_with_every_documented_field()
    {
        var harness = new IngestionHarness();
        await harness.ReviewedCaseAsync();
        var @case = await harness.ApplyAsync(Envelopes.Consultation(
            harness.Clock.UtcNow,
            CareLoop.Domain.Tracks.ConsultationStatus.Requested));
        @case = await harness.ApplyAsync(Envelopes.Consultation(
            harness.Clock.UtcNow,
            CareLoop.Domain.Tracks.ConsultationStatus.OwnerProposed,
            proposedOwner: TestData.Clinician("SYN-STAFF-0002") with { Role = "receiving-clinician" },
            actor: Envelopes.Clinician()));

        var task = new CaseTaskMapper().Map(@case);

        Assert.Equal(@case.Id.ToString("D"), task.Id);
        Assert.Equal(FhirTaskStatus.Requested, task.Status);
        Assert.Equal(@case.Stage, StatusMap.ToStage(task.BusinessStatus));
        Assert.Equal("Practitioner/SYN-STAFF-0001", task.Owner!.Reference);
        Assert.Equal("Practitioner/SYN-STAFF-0001", task.Requester!.Reference);
        Assert.Equal($"DiagnosticReport/{Envelopes.ResultId}", task.Focus!.Reference);
        Assert.Equal($"DiagnosticReport/{Envelopes.ResultId}", Assert.Single(task.BasedOn).Reference);
        Assert.Equal("Patient/SYN-PT-0001", task.For!.Reference);
        Assert.Equal(@case.DueAt, new FhirDateTime(task.Restriction!.Period!.End).ToDateTimeOffset(TimeSpan.Zero));
        Assert.Null(task.StatusReason);
        Assert.Equal(@case.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), task.Meta!.VersionId);
    }

    [Fact]
    public async System.Threading.Tasks.Task Closed_case_task_carries_the_closure_reason_and_evidence()
    {
        var harness = new IngestionHarness();
        await harness.CaseInClosureReviewAsync();
        var closed = await harness.ApplyAsync(Envelopes.Closure(harness.Clock.UtcNow));

        var task = new CaseTaskMapper().Map(closed);

        Assert.Equal(FhirTaskStatus.Completed, task.Status);
        Assert.Equal(ClosureReason.Treated, StatusMap.ToClosureReason(task.StatusReason));
        Assert.Null(task.Restriction);
        var output = Assert.Single(task.Output);
        Assert.Equal("Procedure/SYN-PROC-0001", ((ResourceReference)output.Value).Reference);
    }

    [Fact]
    public async System.Threading.Tasks.Task Case_task_carries_no_patient_name()
    {
        var harness = new IngestionHarness();
        var @case = await harness.ApplyAsync(Envelopes.ScreeningResult(harness.Clock.UtcNow, patientDisplayName: "SYNTHETIC Patient One", patientSecondaryIdentifier: "SYN-MRN-0001"));

        var json = FhirJsonCodec.Serialize(new CaseTaskMapper().Map(@case));

        Assert.DoesNotContain("SYNTHETIC Patient One", json);
        Assert.DoesNotContain("SYN-MRN-0001", json);
    }

    private static HandoffProposed Proposed(Guid caseId) => new()
    {
        EventId = Guid.NewGuid(),
        CaseId = caseId,
        OccurredAt = At,
        CurrentOwner = TestData.Clinician(),
        ProposedOwner = TestData.Clinician("SYN-STAFF-0002"),
        RespondBy = At.AddHours(4),
    };

    private static HandoffSummaryRecord Summary(Guid caseId, HandoffSummaryStatus status) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        Status = status,
        Text = "PLACEHOLDER - synthetic handoff summary",
        ApprovedBy = status == HandoffSummaryStatus.Approved ? "SYN-STAFF-0001" : null,
        ApprovedAt = status == HandoffSummaryStatus.Approved ? At : null,
    };
}
