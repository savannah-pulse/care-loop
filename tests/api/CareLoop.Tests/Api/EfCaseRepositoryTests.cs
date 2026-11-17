using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Api.Infrastructure.Persistence.Audit;
using CareLoop.Api.Infrastructure.Persistence.Outbox;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Domain;
using CareLoop.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Tests.Api;

/// <summary>
/// The single write path stores everything in one transaction.
/// </summary>
/// <remarks>
/// Runs against SQLite in memory, which enforces the unique indexes, the concurrency token and
/// transactions the way PostgreSQL does. Each test gets its own database.
/// </remarks>
public sealed class EfCaseRepositoryTests : IDisposable
{
    private static readonly string[] Subscribers = ["FhirPublisher", "NotificationRouter"];

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeClock _clock = new();

    public EfCaseRepositoryTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task A_case_read_is_stored_in_the_read_audit()
    {
        var caseId = Guid.NewGuid();
        await using (var db = NewContext())
        {
            db.CaseReads.Add(new CaseReadRecord
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                ActorReference = "SYN-CLIN-0001",
                ActorRole = "clinician",
                ActorFacilityId = "SYN-FAC-B",
                View = "case",
                OutsideTeam = true,
                Reason = "SYN covering for a colleague",
                ReadAt = _clock.UtcNow,
            });
            db.CaseReads.Add(new CaseReadRecord
            {
                Id = Guid.NewGuid(),
                CaseId = null,
                ActorReference = "SYN-CLIN-0001",
                ActorRole = "clinician",
                ActorFacilityId = "SYN-FAC-B",
                View = "case-list",
                Query = "patientReference=SYN-PT-0001",
                ReadAt = _clock.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using var verify = NewContext();
        var reads = await verify.CaseReads.AsNoTracking().OrderBy(r => r.View).ToListAsync();
        Assert.Equal(2, reads.Count);
        Assert.Equal(caseId, reads[0].CaseId);
        Assert.True(reads[0].OutsideTeam);
        Assert.Equal("SYN covering for a colleague", reads[0].Reason);
        Assert.Null(reads[1].CaseId);
        Assert.Equal("patientReference=SYN-PT-0001", reads[1].Query);
        Assert.Empty(await verify.AuditEvents.ToListAsync());
    }

    [Fact]
    public async Task Save_persists_event_case_audit_and_outbox_together()
    {
        var @case = NewCaseWithNotification();
        var sourceEvent = TestData.AppliedEvent(@case, "SYN-EVT-1", _clock.UtcNow);

        await using (var db = NewContext())
        {
            await NewRepository(db).SaveAsync(@case, sourceEvent, CancellationToken.None);
        }

        Assert.Empty(@case.DomainEvents);
        Assert.Equal(1, @case.Version);

        await using var verify = NewContext();
        var stored = await verify.Cases.SingleAsync();
        Assert.Equal(@case.Id, stored.Id);
        Assert.Equal(1, stored.Version);
        Assert.NotNull(stored.Notification);

        var storedEvent = await verify.SourceEvents.SingleAsync();
        Assert.Equal(sourceEvent.Id, storedEvent.Id);
        Assert.Equal(@case.Id, storedEvent.CaseId);

        var audit = await verify.AuditEvents.SingleAsync();
        Assert.Equal(sourceEvent.Id, audit.SourceEventId);
        Assert.Null(audit.PriorProjectionJson);
        Assert.Equal(CaseStage.FollowUpActive, CaseProjection.FromJson(audit.NewProjectionJson).Stage);
        Assert.Equal(TestData.SourceSystem, audit.ActorReference);
        Assert.Equal("system", audit.ActorRole);

        var message = await verify.OutboxMessages.Include(m => m.Deliveries).SingleAsync();
        Assert.Equal("NotificationRequested", message.EventType);
        Assert.Equal(@case.Id, message.CaseId);
        Assert.Contains("\"templateKey\":\"patient-follow-up\"", message.PayloadJson);
        Assert.Equal(Subscribers.Length, message.Deliveries.Count);
    }

    [Fact]
    public async Task Update_audit_carries_the_projection_as_loaded_and_as_saved()
    {
        var first = NewCaseWithNotification();
        await using (var db = NewContext())
        {
            await NewRepository(db).SaveAsync(first, TestData.AppliedEvent(first, "SYN-EVT-1", _clock.UtcNow), CancellationToken.None);
        }

        await using (var db = NewContext())
        {
            var repository = NewRepository(db);
            var loaded = (await repository.GetAsync(first.Id, CancellationToken.None))!;
            loaded.RecordOutcomeEvidence("SYN-PROC-0001", _clock.UtcNow);
            loaded.MoveTo(CaseStage.ClosureReview, _clock.UtcNow);
            await repository.SaveAsync(loaded, TestData.AppliedEvent(loaded, "SYN-EVT-2", _clock.UtcNow, CaseEventType.OutcomeRecorded), CancellationToken.None);
        }

        await using var verify = NewContext();
        var audits = await verify.AuditEvents.OrderBy(a => a.SourceEventId == default ? 0 : 1).ToListAsync();
        var update = audits.Single(a => a.EventType == CaseEventType.OutcomeRecorded);
        var opened = audits.Single(a => a.EventType == CaseEventType.ScreeningResultReceived);
        Assert.Equal(opened.NewProjectionJson, update.PriorProjectionJson);
        Assert.Equal(CaseStage.FollowUpActive, CaseProjection.FromJson(update.PriorProjectionJson!).Stage);
        Assert.Equal(CaseStage.ClosureReview, CaseProjection.FromJson(update.NewProjectionJson).Stage);
        Assert.Equal(2, (await verify.Cases.SingleAsync()).Version);
    }

    [Fact]
    public async Task Failure_in_any_part_stores_nothing()
    {
        var first = NewCaseWithNotification();
        var firstEvent = TestData.AppliedEvent(first, "SYN-EVT-1", _clock.UtcNow);
        await using (var db = NewContext())
        {
            await NewRepository(db).SaveAsync(first, firstEvent, CancellationToken.None);
        }

        var second = NewCaseWithNotification("SYN-RESULT-0002");
        var clashingEvent = TestData.AppliedEvent(second, "SYN-EVT-2", _clock.UtcNow) with { Id = firstEvent.Id };

        await using (var db = NewContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NewRepository(db).SaveAsync(second, clashingEvent, CancellationToken.None));
        }

        await using var verify = NewContext();
        Assert.Equal(1, await verify.Cases.CountAsync());
        Assert.Equal(1, await verify.SourceEvents.CountAsync());
        Assert.Equal(1, await verify.AuditEvents.CountAsync());
        Assert.Equal(1, await verify.OutboxMessages.CountAsync());
        Assert.Equal(Subscribers.Length, await verify.OutboxDeliveries.CountAsync());
        Assert.Equal(0, second.Version);
    }

    [Fact]
    public async Task Duplicate_source_event_is_refused_by_the_unique_index()
    {
        var first = NewCaseWithNotification();
        await using (var db = NewContext())
        {
            await NewRepository(db).SaveAsync(first, TestData.AppliedEvent(first, "SYN-EVT-1", _clock.UtcNow), CancellationToken.None);
        }

        var second = NewCaseWithNotification("SYN-RESULT-0002");
        await using (var db = NewContext())
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NewRepository(db).SaveAsync(second, TestData.AppliedEvent(second, "SYN-EVT-1", _clock.UtcNow), CancellationToken.None));
            Assert.Contains("SYN-EVT-1", exception.Message);
            Assert.IsAssignableFrom<DbUpdateException>(exception.InnerException);
        }

        await using var verify = NewContext();
        Assert.Equal(1, await verify.SourceEvents.CountAsync());
        Assert.Equal(1, await verify.Cases.CountAsync());
    }

    [Fact]
    public async Task Stale_case_version_is_refused()
    {
        var @case = NewCaseWithNotification();
        await using (var db = NewContext())
        {
            await NewRepository(db).SaveAsync(@case, TestData.AppliedEvent(@case, "SYN-EVT-1", _clock.UtcNow), CancellationToken.None);
        }

        await using var dbA = NewContext();
        await using var dbB = NewContext();
        var repositoryA = NewRepository(dbA);
        var repositoryB = NewRepository(dbB);
        var copyA = (await repositoryA.GetAsync(@case.Id, CancellationToken.None))!;
        var copyB = (await repositoryB.GetAsync(@case.Id, CancellationToken.None))!;

        copyA.FlagException(CaseExceptions.Overdue, _clock.UtcNow);
        await repositoryA.SaveAsync(copyA, TestData.AppliedEvent(copyA, "SYN-EVT-2", _clock.UtcNow, CaseEventType.DeadlineElapsed), CancellationToken.None);

        copyB.FlagException(CaseExceptions.Unreachable, _clock.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            repositoryB.SaveAsync(copyB, TestData.AppliedEvent(copyB, "SYN-EVT-3", _clock.UtcNow, CaseEventType.NotificationUpdated), CancellationToken.None));

        await using var verify = NewContext();
        Assert.Equal(2, await verify.SourceEvents.CountAsync());
        var stored = await verify.Cases.SingleAsync();
        Assert.Equal(2, stored.Version);
        Assert.Equal(CaseExceptions.Overdue, stored.Exceptions);
    }

    [Fact]
    public async Task Escalation_acknowledged_in_a_later_save_is_stored()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.Escalate(new Escalation { Level = 1, EscalatedTo = TestData.FacilityLeadQueue(), RaisedAt = _clock.UtcNow, Reason = "overdue" });
        await SaveAsync(@case, "SYN-EVT-1");

        _clock.Advance(TimeSpan.FromHours(1));
        await ChangeAsync(@case.Id, "SYN-EVT-2", loaded => loaded.AcknowledgeEscalation(
            new Actor { Reference = "SYN-LEAD-0001", Role = "facility-lead", FacilityId = "SYN-FAC-A" }, _clock.UtcNow));

        var stored = Assert.Single((await LoadAsync(@case.Id)).Escalations);
        Assert.Equal(@case.Escalations[0].Id, stored.Id);
        Assert.Equal("SYN-QUEUE-FAC-A", stored.EscalatedTo.Reference);
        Assert.Equal("SYN-LEAD-0001", stored.AcknowledgedBy!.Reference);
        Assert.Equal(_clock.UtcNow, stored.AcknowledgedAt);
        Assert.True(stored.IsOpen);
    }

    [Fact]
    public async Task Escalations_resolved_in_a_later_save_are_stored_in_order()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        var coordinator = new CaseOwner { Reference = "SYN-COORD", Role = "coordinator", FacilityId = "SYN-FAC-A", IsQueue = true };
        @case.Escalate(new Escalation { Level = 1, EscalatedTo = TestData.FacilityLeadQueue(), RaisedAt = _clock.UtcNow, Reason = "overdue" });
        @case.AcknowledgeEscalation(new Actor { Reference = "SYN-LEAD-0001", Role = "facility-lead", FacilityId = "SYN-FAC-A" }, _clock.UtcNow);
        await SaveAsync(@case, "SYN-EVT-1");

        _clock.Advance(TimeSpan.FromHours(1));
        await ChangeAsync(@case.Id, "SYN-EVT-2", loaded => loaded.Escalate(new Escalation { Level = 2, EscalatedTo = coordinator, RaisedAt = _clock.UtcNow, Reason = "still overdue" }));

        _clock.Advance(TimeSpan.FromHours(1));
        await ChangeAsync(@case.Id, "SYN-EVT-3", loaded => loaded.ResolveEscalations(_clock.UtcNow));

        var loaded = await LoadAsync(@case.Id);
        Assert.False(loaded.IsEscalated);
        Assert.Equal([1, 2], loaded.Escalations.Select(e => e.Level));
        Assert.All(loaded.Escalations, e => Assert.Equal(_clock.UtcNow, e.ResolvedAt));
        Assert.Equal(["SYN-QUEUE-FAC-A", "SYN-COORD"], loaded.Escalations.Select(e => e.EscalatedTo.Reference));
        Assert.Equal("SYN-LEAD-0001", loaded.Escalations[0].AcknowledgedBy!.Reference);
        Assert.Null(loaded.Escalations[1].AcknowledgedBy);
    }

    [Fact]
    public async Task Consent_withdrawn_in_a_later_save_is_stored()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.RecordConsent(new ContactPoint { Channel = NotificationChannel.Sms, Address = "SYN-CAREGIVER-INBOX-0001" }, "Consent/SYN-CONSENT-0001", _clock.UtcNow);
        await using (var db = NewContext())
        {
            await NewRepository(db).SaveAsync(@case, TestData.AppliedEvent(@case, "SYN-EVT-1", _clock.UtcNow, CaseEventType.ConsentRecorded), CancellationToken.None);
        }

        _clock.Advance(TimeSpan.FromHours(1));
        await using (var db = NewContext())
        {
            var repository = NewRepository(db);
            var loaded = (await repository.GetAsync(@case.Id, CancellationToken.None))!;
            loaded.WithdrawConsent("Consent/SYN-CONSENT-0001/_history/2", _clock.UtcNow);

            await repository.SaveAsync(loaded, TestData.AppliedEvent(loaded, "SYN-EVT-2", _clock.UtcNow, CaseEventType.ConsentRecorded), CancellationToken.None);

            Assert.Equal("SYN-CAREGIVER-INBOX-0001", loaded.CaregiverConsent!.CaregiverContactPoint.Address);
        }

        await using var verify = NewContext();
        var stored = (await NewRepository(verify).GetAsync(@case.Id, CancellationToken.None))!.CaregiverConsent!;
        Assert.False(stored.IsActive);
        Assert.Equal(_clock.UtcNow, stored.WithdrawnAt);
        Assert.Equal("Consent/SYN-CONSENT-0001/_history/2", stored.WithdrawalEvidenceReference);
        Assert.Equal("Consent/SYN-CONSENT-0001", stored.EvidenceReference);
        Assert.Equal(NotificationChannel.Sms, stored.CaregiverContactPoint.Channel);
        Assert.Equal(2, await verify.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Case_round_trips_with_tracks_escalations_and_closure()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.Notification!.RecordAttempt(new NotificationAttempt
        {
            Id = Guid.NewGuid(),
            Channel = NotificationChannel.Sms,
            AttemptedAt = _clock.UtcNow,
            Status = NotificationAttemptStatus.Delivered,
            UsedSmsSafeTemplate = false,
            ProviderMessageId = "SYN-MSG-0001",
        });
        @case.Notification.RecordCallRequest(_clock.UtcNow, "SYN-COMM-0002");
        @case.Notification.RateConfidence(3, _clock.UtcNow, "SYN-COMM-0004");
        @case.Notification.Advance(NotificationStatus.Acknowledged, _clock.UtcNow, "SYN-COMM-0003");
        @case.RecordConsent(new ContactPoint { Channel = NotificationChannel.Email, Address = "SYN-CAREGIVER-INBOX-0001" }, "Consent/SYN-CONSENT-0001", _clock.UtcNow);
        @case.WithdrawConsent("Consent/SYN-CONSENT-0001/_history/2", _clock.UtcNow);
        @case.BeginConsultation("SYN-SR-0001", _clock.UtcNow).Advance(ConsultationStatus.Accepted, _clock.UtcNow, "SYN-TASK-0001");
        @case.BeginReferral("SYN-REF-0001", _clock.UtcNow).Reject("PLACEHOLDER: wrong service", _clock.UtcNow, "SYN-TASK-0002");
        @case.BeginAppointment("SYN-APPT-0001", _clock.UtcNow.AddDays(2), _clock.UtcNow).Advance(AppointmentStatus.Attended, _clock.UtcNow, null);
        @case.Escalate(new Escalation { Level = 1, EscalatedTo = TestData.FacilityLeadQueue(), RaisedAt = _clock.UtcNow, Reason = "overdue" });
        @case.Escalate(new Escalation { Level = 2, EscalatedTo = new CaseOwner { Reference = "SYN-COORD", Role = "coordinator", FacilityId = "SYN-FAC-A", IsQueue = true }, RaisedAt = _clock.UtcNow, Reason = "still overdue" });
        @case.AcknowledgeEscalation(new Actor { Reference = "SYN-COORD-0001", Role = "coordinator", FacilityId = "SYN-FAC-A" }, _clock.UtcNow);
        @case.RecordOutcomeEvidence("SYN-PROC-0001", _clock.UtcNow);
        @case.MoveTo(CaseStage.ClosureReview, _clock.UtcNow);
        var decision = new ClosureDecision
        {
            Reason = ClosureReason.OnSurveillance,
            AuthorizedBy = DomainTestData.ClinicianActor(),
            DecidedAt = _clock.UtcNow,
            EvidenceReference = "SYN-PROC-0001",
            NextSurveillanceDate = new DateOnly(2027, 3, 1),
            DocumentedAttemptReferences = ["SYN-COMM-0001", "SYN-COMM-0002"],
        };
        @case.Close(decision, _clock.UtcNow);

        await using (var db = NewContext())
        {
            await NewRepository(db).SaveAsync(@case, TestData.AppliedEvent(@case, "SYN-EVT-1", _clock.UtcNow, CaseEventType.ClosureDecided), CancellationToken.None);
        }

        await using var verify = NewContext();
        var loaded = (await NewRepository(verify).GetAsync(@case.Id, CancellationToken.None))!;

        Assert.Equal(CaseStage.Closed, loaded.Stage);
        Assert.True(loaded.IsClosed);
        Assert.Equal("PLACEHOLDER-URGENCY-A", loaded.Urgency);
        Assert.Equal(@case.Owner, loaded.Owner);
        Assert.Equal(NotificationStatus.Acknowledged, loaded.Notification!.Status);
        Assert.True(loaded.Notification.CallRequested);
        Assert.Equal(3, loaded.Notification.Confidence);
        Assert.Equal(@case.CaregiverConsent, loaded.CaregiverConsent);
        Assert.False(loaded.CaregiverConsent!.IsActive);
        Assert.Equal("SYN-CAREGIVER-INBOX-0001", loaded.CaregiverConsent.CaregiverContactPoint.Address);
        var attempt = Assert.Single(loaded.Notification.Attempts);
        Assert.Equal("SYN-MSG-0001", attempt.ProviderMessageId);
        Assert.Equal(ConsultationStatus.Accepted, loaded.Consultation!.Status);
        Assert.Equal(ReferralStatus.Rejected, loaded.Referral!.Status);
        Assert.Equal("PLACEHOLDER: wrong service", loaded.Referral.RejectionReason);
        Assert.Equal(AppointmentStatus.Attended, loaded.Appointment!.Status);
        Assert.Equal("SYN-PROC-0001", loaded.Appointment.OutcomeReference);
        Assert.True(loaded.Appointment.IsComplete());
        Assert.Equal(2, loaded.Escalations.Count);
        Assert.All(loaded.Escalations, e => Assert.NotNull(e.ResolvedAt));
        Assert.Equal("SYN-COORD", loaded.Escalations[1].EscalatedTo.Reference);
        Assert.Equal(@case.Escalations.Select(e => e.Id), loaded.Escalations.Select(e => e.Id));
        Assert.All(loaded.Escalations, e => Assert.NotEqual(Guid.Empty, e.Id));
        Assert.Null(loaded.Escalations[0].AcknowledgedBy);
        Assert.Equal("SYN-COORD-0001", loaded.Escalations[1].AcknowledgedBy!.Reference);
        Assert.Equal(_clock.UtcNow, loaded.Escalations[1].AcknowledgedAt);
        Assert.False(loaded.IsEscalated);
        Assert.NotNull(loaded.Closure);
        Assert.Equal(decision, loaded.Closure with { DocumentedAttemptReferences = decision.DocumentedAttemptReferences });
        Assert.Equal(["SYN-COMM-0001", "SYN-COMM-0002"], loaded.Closure.DocumentedAttemptReferences);
        Assert.Equal(new DateOnly(2027, 3, 1), loaded.Closure.NextSurveillanceDate);
        Assert.Null(loaded.NextAction);
        Assert.Null(loaded.PendingAssignment);
    }

    [Fact]
    public async Task Patient_name_and_record_number_round_trip_and_stay_out_of_the_audit_trail()
    {
        var @case = TestData.NewCase(_clock.UtcNow, "SYN-RESULT-0020", patientDisplayName: "SYN Patient One", patientSecondaryIdentifier: "SYN-MRN-0001");

        await using (var db = NewContext())
        {
            await NewRepository(db).SaveAsync(@case, TestData.AppliedEvent(@case, "SYN-EVT-20", _clock.UtcNow), CancellationToken.None);
        }

        await using var verify = NewContext();
        var loaded = (await NewRepository(verify).GetAsync(@case.Id, CancellationToken.None))!;
        var audit = await verify.AuditEvents.SingleAsync(a => a.CaseId == @case.Id);

        Assert.Equal("SYN Patient One", loaded.PatientDisplayName);
        Assert.Equal("SYN-MRN-0001", loaded.PatientSecondaryIdentifier);
        Assert.DoesNotContain("SYN Patient One", audit.NewProjectionJson);
        Assert.DoesNotContain("SYN-MRN-0001", audit.NewProjectionJson);
    }

    [Fact]
    public async Task Pending_assignment_round_trips()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.ProposeOwner(new Assignment
        {
            ProposedOwner = TestData.Clinician("SYN-STAFF-0002"),
            ProposedBy = DomainTestData.ClinicianActor(),
            ProposedAt = _clock.UtcNow,
            RespondBy = _clock.UtcNow.AddDays(1),
            Status = AssignmentStatus.Proposed,
        });

        await using (var db = NewContext())
        {
            await NewRepository(db).SaveAsync(@case, TestData.AppliedEvent(@case, "SYN-EVT-1", _clock.UtcNow, CaseEventType.ConsultationUpdated), CancellationToken.None);
        }

        await using var verify = NewContext();
        var loaded = (await NewRepository(verify).GetAsync(@case.Id, CancellationToken.None))!;
        Assert.Equal("SYN-STAFF-0002", loaded.PendingAssignment!.ProposedOwner.Reference);
        Assert.Equal(AssignmentStatus.Proposed, loaded.PendingAssignment.Status);
        Assert.Equal(_clock.UtcNow.AddDays(1), loaded.PendingAssignment.RespondBy);
    }

    [Fact]
    public async Task One_outbox_delivery_row_is_created_per_subscriber()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.ClearDomainEvents();
        @case.Escalate(new Escalation { Level = 1, EscalatedTo = TestData.FacilityLeadQueue(), RaisedAt = _clock.UtcNow, Reason = "overdue" });
        @case.BeginReferral("SYN-REF-0001", _clock.UtcNow).Reject("PLACEHOLDER", _clock.UtcNow, null);
        @case.Raise(new CareLoop.Domain.Events.DomainEvents.ReferralRejected
        {
            EventId = Guid.NewGuid(),
            CaseId = @case.Id,
            OccurredAt = _clock.UtcNow,
            CurrentOwner = @case.Owner,
            Reason = "PLACEHOLDER",
        });
        string[] three = ["FhirPublisher", "NotificationRouter", "SYN-AuditMirror"];

        await using (var db = NewContext())
        {
            var repository = new EfCaseRepository(db, _clock, new OutboxSubscriberRegistry(() => three));
            await repository.SaveAsync(@case, TestData.AppliedEvent(@case, "SYN-EVT-1", _clock.UtcNow, CaseEventType.ReferralUpdated), CancellationToken.None);
        }

        await using var verify = NewContext();
        var messages = await verify.OutboxMessages.Include(m => m.Deliveries).ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Equal(6, await verify.OutboxDeliveries.CountAsync());
        foreach (var message in messages)
        {
            Assert.Equal(three.Order(), message.Deliveries.Select(d => d.Subscriber).Order());
            Assert.All(message.Deliveries, d => Assert.Equal(OutboxDeliveryStatus.Pending, d.Status));
            Assert.All(message.Deliveries, d => Assert.Equal(0, d.Attempts));
        }

        Assert.Contains(messages, m => m.EventType == "EscalationRaised");
        Assert.Contains(messages, m => m.EventType == "ReferralRejected");
    }

    [Fact]
    public async Task Find_by_source_returns_the_case_for_a_result()
    {
        var @case = NewCaseWithNotification("SYN-RESULT-0042");
        await using (var db = NewContext())
        {
            await NewRepository(db).SaveAsync(@case, TestData.AppliedEvent(@case, "SYN-EVT-1", _clock.UtcNow), CancellationToken.None);
        }

        await using var verify = NewContext();
        var repository = NewRepository(verify);
        var found = await repository.FindBySourceAsync(TestData.SourceSystem, "SYN-RESULT-0042", CancellationToken.None);
        var missing = await repository.FindBySourceAsync(TestData.SourceSystem, "SYN-RESULT-UNKNOWN", CancellationToken.None);

        Assert.Equal(@case.Id, found!.Id);
        Assert.Null(missing);
        Assert.True(await repository.EventExistsAsync(TestData.SourceSystem, "SYN-EVT-1", CancellationToken.None));
        Assert.False(await repository.EventExistsAsync(TestData.SourceSystem, "SYN-EVT-2", CancellationToken.None));
    }

    [Fact]
    public async Task Event_for_another_case_is_refused_before_anything_is_written()
    {
        var @case = NewCaseWithNotification();
        var other = NewCaseWithNotification("SYN-RESULT-0002");

        await using var db = NewContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewRepository(db).SaveAsync(@case, TestData.AppliedEvent(other, "SYN-EVT-1", _clock.UtcNow), CancellationToken.None));

        Assert.Equal(0, await db.Cases.CountAsync());
    }

    private async Task SaveAsync(Case @case, string eventId)
    {
        await using var db = NewContext();
        await NewRepository(db).SaveAsync(@case, TestData.AppliedEvent(@case, eventId, _clock.UtcNow), CancellationToken.None);
    }

    private async Task ChangeAsync(Guid caseId, string eventId, Action<Case> change)
    {
        await using var db = NewContext();
        var repository = NewRepository(db);
        var loaded = (await repository.GetAsync(caseId, CancellationToken.None))!;
        change(loaded);
        await repository.SaveAsync(loaded, TestData.AppliedEvent(loaded, eventId, _clock.UtcNow), CancellationToken.None);
    }

    private async Task<Case> LoadAsync(Guid caseId)
    {
        await using var db = NewContext();
        return (await NewRepository(db).GetAsync(caseId, CancellationToken.None))!;
    }

    private CareLoopDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CareLoopDbContext>().UseSqlite(_connection).Options);

    private EfCaseRepository NewRepository(CareLoopDbContext db) =>
        new(db, _clock, new OutboxSubscriberRegistry(() => Subscribers));

    /// <summary>A reviewed case in follow-up whose notification request is still waiting to go to the outbox.</summary>
    private Case NewCaseWithNotification(string resultId = "SYN-RESULT-0001")
    {
        var @case = TestData.NewCase(_clock.UtcNow, resultId);
        @case.MoveTo(CaseStage.ReviewPending, _clock.UtcNow);
        @case.RecordReview("PLACEHOLDER-URGENCY-A", DomainTestData.ClinicianActor(), _clock.UtcNow);
        @case.MoveTo(CaseStage.FollowUpActive, _clock.UtcNow);
        @case.BeginNotification("patient-follow-up", _clock.UtcNow);
        @case.SetNextAction("acknowledge-notification", _clock.UtcNow.AddDays(1), _clock.UtcNow);
        return @case;
    }
}
