using System.Net;
using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Fhir.Outbound;
using CareLoop.Api.Infrastructure.Persistence.Summaries;
using CareLoop.Contracts.Summaries;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Tests.Fakes;
using Hl7.Fhir.Model;
using static CareLoop.Tests.Api.Fhir.FhirTestSupport;
using FhirTask = Hl7.Fhir.Model.Task;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// FhirPublisher turns domain events into FHIR resources sent to the case's source system.
/// </summary>
public sealed class FhirPublisherTests : IDisposable
{
    private readonly FhirStore _store = new();
    private readonly RecordingHandler _http = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async System.Threading.Tasks.Task NotificationRequested_publishes_nothing()
    {
        var @case = await StoredCaseAsync();

        await PublishAsync(new NotificationRequested { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, PatientReference = "SYN-PT-0001", TemplateKey = "patient-follow-up" });

        Assert.Empty(_http.Requests);
    }

    [Fact]
    public async System.Threading.Tasks.Task HandoffProposed_sends_only_the_case_Task_even_when_an_approved_summary_exists()
    {
        var @case = await StoredCaseAsync();
        await using (var db = _store.NewContext())
        {
            db.HandoffSummaries.Add(Summary(@case.Id, HandoffSummaryStatus.Approved));
            await db.SaveChangesAsync();
        }

        await PublishAsync(Proposed(@case.Id));

        var request = Assert.Single(_http.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal(FhirEndpoints.FhirJson, request.ContentType);
        Assert.Equal($"http://his.invalid/fhir/Task/{@case.Id:D}", request.Uri.ToString());
        var task = Assert.IsType<FhirTask>(Assert.Single(_http.Sent));
        Assert.Equal(StatusMap.CaseTaskCode, StatusMap.CodeIn(task.Code, StatusMap.TaskCodeSystem));
    }

    [Fact]
    public async System.Threading.Tasks.Task HandoffSummaryApproved_sends_a_DocumentReference_for_exactly_that_summary()
    {
        var @case = await StoredCaseAsync();
        var older = Summary(@case.Id, HandoffSummaryStatus.Approved);
        var approved = Summary(@case.Id, HandoffSummaryStatus.Approved);
        await using (var db = _store.NewContext())
        {
            db.HandoffSummaries.AddRange(older, approved, Summary(@case.Id, HandoffSummaryStatus.Draft));
            await db.SaveChangesAsync();
        }

        await PublishAsync(Approval(@case.Id, approved.Id));

        var request = Assert.Single(_http.Requests);
        Assert.Equal($"http://his.invalid/fhir/DocumentReference/handoff-{approved.Id:N}", request.Uri.ToString());
        Assert.IsType<DocumentReference>(Assert.Single(_http.Sent));
    }

    [Fact]
    public async System.Threading.Tasks.Task HandoffSummaryApproved_for_a_missing_summary_fails_so_the_outbox_retries()
    {
        var @case = await StoredCaseAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => PublishAsync(Approval(@case.Id, Guid.NewGuid())));
        Assert.Empty(_http.Requests);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeadlineApproaching_publishes_nothing()
    {
        var @case = await StoredCaseAsync();

        await PublishAsync(new DeadlineApproaching { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = @case.Owner, NextAction = "review-result", DueAt = _store.Clock.UtcNow.AddHours(1) });

        Assert.Empty(_http.Requests);
    }

    [Fact]
    public async System.Threading.Tasks.Task AppointmentScheduled_publishes_nothing()
    {
        var @case = await StoredCaseAsync();

        await PublishAsync(new AppointmentScheduled { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, ScheduledFor = _store.Clock.UtcNow.AddDays(3), Location = "SYN Breast Imaging Clinic" });

        Assert.Empty(_http.Requests);
    }

    [Fact]
    public async System.Threading.Tasks.Task CaseClosed_sends_the_case_task_completed()
    {
        var @case = await StoredCaseAsync();

        await PublishAsync(new CaseClosed { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, Reason = ClosureReason.Treated, AuthorizedBy = Envelopes.Clinician() });

        var task = Assert.IsType<FhirTask>(Assert.Single(_http.Sent));
        Assert.Equal(@case.Id.ToString("D"), task.Id);
    }

    [Fact]
    public async System.Threading.Tasks.Task Escalation_and_call_request_send_notice_tasks()
    {
        var @case = await StoredCaseAsync();

        await PublishAsync(new EscalationRaised { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, Level = 1, EscalatedTo = TestData.FacilityLeadQueue(), CurrentOwner = @case.Owner, Reason = "overdue" });
        await PublishAsync(new CallRequested { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = @case.Owner, DueAt = _store.Clock.UtcNow.AddHours(2) });

        Assert.Equal(
            [StatusMap.EscalationTaskCode, StatusMap.CallRequestTaskCode],
            _http.Sent.Cast<FhirTask>().Select(t => StatusMap.CodeIn(t.Code, StatusMap.TaskCodeSystem)));
    }

    [Fact]
    public async System.Threading.Tasks.Task Handling_the_same_event_twice_sends_the_same_resource_id()
    {
        var @case = await StoredCaseAsync();
        var escalation = new EscalationRaised { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, Level = 1, EscalatedTo = TestData.FacilityLeadQueue(), CurrentOwner = @case.Owner, Reason = "overdue" };

        await PublishAsync(escalation);
        await PublishAsync(escalation);

        Assert.Equal(2, _http.Requests.Count);
        Assert.Single(_http.Requests.Select(r => r.Uri).Distinct());
    }

    [Fact]
    public async System.Threading.Tasks.Task Unconfigured_source_system_fails_so_the_outbox_retries()
    {
        var @case = await StoredCaseAsync();
        await using var db = _store.NewContext();
        var publisher = new FhirPublisher(UnconfiguredClient(), new EscalationTaskMapper(), new HandoffDocumentReferenceMapper(), new CaseTaskMapper(), db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.HandleAsync(new CaseClosed { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, Reason = ClosureReason.Treated, AuthorizedBy = Envelopes.Clinician() }, CancellationToken.None));
    }

    [Fact]
    public async System.Threading.Tasks.Task Refusal_by_the_system_fails_so_the_outbox_retries()
    {
        var @case = await StoredCaseAsync();
        var refusing = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await using var db = _store.NewContext();
        var publisher = new FhirPublisher(Client(refusing), new EscalationTaskMapper(), new HandoffDocumentReferenceMapper(), new CaseTaskMapper(), db);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            publisher.HandleAsync(new CaseClosed { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, Reason = ClosureReason.Treated, AuthorizedBy = Envelopes.Clinician() }, CancellationToken.None));
    }

    private async System.Threading.Tasks.Task<Case> StoredCaseAsync()
    {
        var @case = new Case(Guid.NewGuid(), SourceSystemId, "SYN-RESULT-0001", "SYN-PT-0001", "breast-screening", "SYN-FAC-A", TestData.Clinician(), _store.Clock.UtcNow);
        await _store.SaveAsync(@case);
        return @case;
    }

    private async System.Threading.Tasks.Task PublishAsync(IDomainEvent domainEvent)
    {
        await using var db = _store.NewContext();
        var publisher = new FhirPublisher(Client(_http), new EscalationTaskMapper(), new HandoffDocumentReferenceMapper(), new CaseTaskMapper(), db);
        await publisher.HandleAsync(domainEvent, CancellationToken.None);
    }

    private HandoffProposed Proposed(Guid caseId) => new()
    {
        EventId = Guid.NewGuid(),
        CaseId = caseId,
        OccurredAt = _store.Clock.UtcNow,
        CurrentOwner = TestData.Clinician(),
        ProposedOwner = TestData.Clinician("SYN-STAFF-0002"),
        RespondBy = _store.Clock.UtcNow.AddHours(4),
    };

    private HandoffSummaryApproved Approval(Guid caseId, Guid summaryId) => new()
    {
        EventId = Guid.NewGuid(),
        CaseId = caseId,
        OccurredAt = _store.Clock.UtcNow,
        SummaryId = summaryId,
        ApprovedBy = Envelopes.Clinician(),
    };

    private HandoffSummaryRecord Summary(Guid caseId, HandoffSummaryStatus status)
    {
        return new HandoffSummaryRecord
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            RequestId = Guid.NewGuid(),
            Status = status,
            Text = "PLACEHOLDER - synthetic handoff summary",
            DraftedAt = _store.Clock.UtcNow,
            ApprovedBy = status == HandoffSummaryStatus.Approved ? "SYN-STAFF-0001" : null,
            ApprovedAt = status == HandoffSummaryStatus.Approved ? _store.Clock.UtcNow : null,
        };
    }
}
