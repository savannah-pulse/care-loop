using CareLoop.Api.Features.Events;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Api;

/// <summary>
/// One intake pipeline: validate, deduplicate, append, apply, audit.
/// </summary>
public sealed class EventIngestorTests
{
    private readonly IngestionHarness _harness = new();

    [Fact]
    public async Task Replaying_the_same_source_event_produces_one_case_and_one_event()
    {
        var envelope = Envelopes.ScreeningResult(_harness.Clock.UtcNow, namedOwner: Envelopes.Clinician());

        var first = await _harness.IngestAsync(envelope);
        var second = await _harness.IngestAsync(envelope);

        Assert.Equal(IngestOutcome.Applied, first.Outcome);
        Assert.Equal(IngestOutcome.Duplicate, second.Outcome);
        Assert.Equal(first.CaseId, second.CaseId);
        Assert.Single(_harness.Repository.Cases);
        Assert.Single(_harness.Repository.Events);
    }

    [Fact]
    public async Task Duplicate_event_returns_duplicate_and_changes_nothing()
    {
        await _harness.OpenCaseAsync();
        var review = Envelopes.ClinicalReview(_harness.Clock.UtcNow);
        await _harness.ApplyAsync(review);
        var @case = _harness.TheCase();
        var updatedAt = @case.UpdatedAt;
        var events = _harness.Repository.Events.Count;
        var audits = _harness.Repository.AuditEvents.Count;
        _harness.Clock.Advance(TimeSpan.FromHours(2));

        var result = await _harness.IngestAsync(review);

        Assert.Equal(IngestOutcome.Duplicate, result.Outcome);
        Assert.Equal(updatedAt, @case.UpdatedAt);
        Assert.Equal(events, _harness.Repository.Events.Count);
        Assert.Equal(audits, _harness.Repository.AuditEvents.Count);
    }

    [Fact]
    public async Task Screening_result_opens_a_case()
    {
        var result = await _harness.IngestAsync(Envelopes.ScreeningResult(_harness.Clock.UtcNow, namedOwner: Envelopes.Clinician()));

        Assert.Equal(IngestOutcome.Applied, result.Outcome);
        var @case = _harness.TheCase();
        Assert.Equal(result.CaseId, @case.Id);
        Assert.Equal(CaseStage.ReviewPending, @case.Stage);
        Assert.Equal("review-result", @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.DueAt);
        Assert.Equal("SYN-STAFF-0001", @case.Owner.Reference);
        Assert.Equal(CaseEventType.ScreeningResultReceived, Assert.Single(_harness.Repository.Events).Type);
    }

    [Fact]
    public async Task Event_for_an_unknown_result_is_invalid()
    {
        var result = await _harness.IngestAsync(Envelopes.ClinicalReview(_harness.Clock.UtcNow, resultId: "SYN-RESULT-UNKNOWN"));

        Assert.Equal(IngestOutcome.Invalid, result.Outcome);
        Assert.Null(result.CaseId);
        Assert.Contains("SYN-RESULT-UNKNOWN", result.Reason);
        Assert.Empty(_harness.Repository.Events);
        Assert.Empty(_harness.Repository.Cases);
    }

    [Fact]
    public async Task Envelope_with_mismatched_payload_type_is_invalid()
    {
        await _harness.OpenCaseAsync();
        var mismatched = Envelopes.ClinicalReview(_harness.Clock.UtcNow) with
        {
            Payload = new OutcomePayload { EvidenceReference = "Procedure/SYN-PROC-0001", EvidenceKind = "PLACEHOLDER" },
        };
        var events = _harness.Repository.Events.Count;

        var result = await _harness.IngestAsync(mismatched);

        Assert.Equal(IngestOutcome.Invalid, result.Outcome);
        Assert.Contains("ClinicalReviewPayload", result.Reason);
        Assert.Equal(events, _harness.Repository.Events.Count);
    }

    [Fact]
    public async Task Internal_event_types_cannot_be_ingested()
    {
        await _harness.OpenCaseAsync();
        var internalEvent = Envelopes.Outcome(_harness.Clock.UtcNow) with { Type = CaseEventType.DeadlineElapsed };

        var result = await _harness.IngestAsync(internalEvent);

        Assert.Equal(IngestOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task Rejected_event_is_stored_and_audited_but_leaves_the_case_unchanged()
    {
        await _harness.ReviewedCaseAsync();
        var @case = _harness.TheCase();
        var stage = @case.Stage;
        var updatedAt = @case.UpdatedAt;

        var result = await _harness.IngestAsync(Envelopes.Closure(_harness.Clock.UtcNow, evidenceReference: null));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
        Assert.Equal(@case.Id, result.CaseId);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        Assert.Equal(stage, @case.Stage);
        Assert.Equal(updatedAt, @case.UpdatedAt);
        Assert.Null(@case.Closure);

        var stored = _harness.Repository.Events.Last();
        Assert.Equal(SourceEventDisposition.Rejected, stored.Disposition);
        Assert.Equal(result.Reason, stored.DispositionReason);
        var audit = _harness.Repository.AuditEvents.Last();
        Assert.Equal(SourceEventDisposition.Rejected, audit.Disposition);
        Assert.Equal(audit.PriorProjectionJson, audit.NewProjectionJson);
        Assert.Equal(result.Reason, audit.Reason);
    }

    [Fact]
    public async Task Applied_event_writes_an_audit_record_with_prior_and_new_projection()
    {
        await _harness.OpenCaseAsync();

        await _harness.ApplyAsync(Envelopes.ClinicalReview(_harness.Clock.UtcNow));

        Assert.Equal(2, _harness.Repository.AuditEvents.Count);
        var opened = _harness.Repository.AuditEvents[0];
        var reviewed = _harness.Repository.AuditEvents[1];
        Assert.Null(opened.PriorProjectionJson);
        Assert.Equal(opened.NewProjectionJson, reviewed.PriorProjectionJson);
        Assert.Equal(CaseStage.ReviewPending, CaseProjection.FromJson(reviewed.PriorProjectionJson!).Stage);
        Assert.Equal(CaseStage.FollowUpActive, CaseProjection.FromJson(reviewed.NewProjectionJson).Stage);
        Assert.Equal("SYN-STAFF-0001", reviewed.ActorReference);
        Assert.Equal("clinician", reviewed.ActorRole);
        Assert.Equal(CaseEventType.ClinicalReviewCompleted, reviewed.EventType);
    }

    [Fact]
    public async Task Domain_events_are_written_to_the_outbox_in_the_same_save()
    {
        await _harness.OpenCaseAsync();

        var @case = await _harness.ApplyAsync(Envelopes.ClinicalReview(_harness.Clock.UtcNow));

        var requested = Assert.Single(_harness.Repository.PublishedDomainEvents.OfType<NotificationRequested>());
        Assert.Equal(@case.Id, requested.CaseId);
        Assert.Empty(@case.DomainEvents);
    }

    [Fact]
    public async Task Received_timestamp_comes_from_the_clock()
    {
        var occurredAt = new DateTimeOffset(2025, 12, 31, 23, 0, 0, TimeSpan.Zero);
        _harness.Clock.Set(new DateTimeOffset(2026, 2, 1, 9, 30, 0, TimeSpan.Zero));

        await _harness.ApplyAsync(Envelopes.ScreeningResult(occurredAt, namedOwner: Envelopes.Clinician()));

        var stored = Assert.Single(_harness.Repository.Events);
        Assert.Equal(_harness.Clock.UtcNow, stored.ReceivedAt);
        Assert.Equal(occurredAt, stored.OccurredAt);
        Assert.Equal(_harness.Clock.UtcNow, _harness.Repository.AuditEvents.Single().ReceivedTimestamp);
    }

    [Fact]
    public async Task Event_the_case_state_cannot_accept_is_rejected_not_crashed()
    {
        await _harness.ReviewedCaseAsync();
        await _harness.ApplyAsync(Envelopes.Appointment(_harness.Clock.UtcNow, AppointmentStatus.Attended));

        var result = await _harness.IngestAsync(Envelopes.Appointment(_harness.Clock.UtcNow, AppointmentStatus.Missed));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
        Assert.Contains("cannot move", result.Reason);
        Assert.Equal(AppointmentStatus.Attended, _harness.TheCase().Appointment!.Status);
    }
}
