using CareLoop.Api.Features.Events;
using CareLoop.Api.Features.Events.Handlers;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Api;

/// <summary>
/// Each of the nine handlers applies its event through the Domain rules.
/// </summary>
public sealed class EventHandlerTests
{
    private readonly IngestionHarness _harness = new();

    [Fact]
    public async Task Screening_result_with_no_named_owner_lands_in_the_facility_lead_queue()
    {
        var @case = await _harness.ApplyAsync(Envelopes.ScreeningResult(_harness.Clock.UtcNow, namedOwner: null));

        Assert.True(@case.Owner.IsQueue);
        Assert.Equal("facility-lead", @case.Owner.Role);
        Assert.Equal("SYN-FAC-A", @case.Owner.FacilityId);
        Assert.True(@case.Exceptions.HasFlag(CaseExceptions.Unassigned));
        Assert.Equal("assign-owner", @case.NextAction);
        Assert.NotNull(@case.DueAt);
    }

    [Fact]
    public async Task Screening_result_keeps_the_patient_name_and_record_number_for_staff_but_not_in_the_audit_snapshot()
    {
        var @case = await _harness.ApplyAsync(Envelopes.ScreeningResult(_harness.Clock.UtcNow, namedOwner: Envelopes.Clinician(),
            patientDisplayName: "SYN Patient One", patientSecondaryIdentifier: "SYN-MRN-0001"));

        Assert.Equal("SYN Patient One", @case.PatientDisplayName);
        Assert.Equal("SYN-MRN-0001", @case.PatientSecondaryIdentifier);
        var audit = Assert.Single(_harness.Repository.AuditEvents);
        Assert.DoesNotContain("SYN Patient One", audit.NewProjectionJson);
        Assert.DoesNotContain("SYN-MRN-0001", audit.NewProjectionJson);
    }

    [Fact]
    public async Task Screening_result_without_demographics_leaves_them_empty()
    {
        var @case = await _harness.ApplyAsync(Envelopes.ScreeningResult(_harness.Clock.UtcNow, namedOwner: Envelopes.Clinician()));

        Assert.Null(@case.PatientDisplayName);
        Assert.Null(@case.PatientSecondaryIdentifier);
    }

    [Fact]
    public async Task Second_screening_result_for_the_same_result_is_refused()
    {
        await _harness.OpenCaseAsync();

        var result = await _harness.IngestAsync(Envelopes.ScreeningResult(_harness.Clock.UtcNow, namedOwner: Envelopes.Clinician()));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
        Assert.Single(_harness.Repository.Cases);
        Assert.Equal(2, _harness.Repository.Events.Count);
        Assert.Equal(SourceEventDisposition.Rejected, _harness.Repository.Events.Last().Disposition);
    }

    [Fact]
    public async Task Screening_result_for_an_unknown_pathway_is_invalid()
    {
        var result = await _harness.IngestAsync(Envelopes.ScreeningResult(_harness.Clock.UtcNow, pathwayId: "SYN-UNKNOWN-PATHWAY"));

        Assert.Equal(IngestOutcome.Invalid, result.Outcome);
        Assert.Empty(_harness.Repository.Cases);
    }

    [Fact]
    public async Task Clinical_review_stores_urgency_and_requests_notification()
    {
        await _harness.OpenCaseAsync();

        var @case = await _harness.ApplyAsync(Envelopes.ClinicalReview(_harness.Clock.UtcNow, urgency: "PLACEHOLDER-URGENCY-B"));

        Assert.Equal("PLACEHOLDER-URGENCY-B", @case.Urgency);
        Assert.Equal(CaseStage.FollowUpActive, @case.Stage);
        Assert.NotNull(@case.Notification);
        Assert.Equal("acknowledge-notification", @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.DueAt);
        var requested = Assert.Single(_harness.Repository.PublishedDomainEvents.OfType<NotificationRequested>());
        Assert.Equal(ClinicalReviewHandler.PatientFollowUpTemplate, requested.TemplateKey);
        Assert.Equal(@case.PatientReference, requested.PatientReference);
    }

    [Fact]
    public async Task Review_with_an_unknown_urgency_code_is_refused()
    {
        await _harness.OpenCaseAsync();

        var result = await _harness.IngestAsync(Envelopes.ClinicalReview(_harness.Clock.UtcNow, urgency: "SYN-NOT-A-CODE"));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
        Assert.Contains("SYN-NOT-A-CODE", result.Reason);
        var @case = _harness.TheCase();
        Assert.Null(@case.Urgency);
        Assert.Equal(CaseStage.ReviewPending, @case.Stage);
        Assert.Null(@case.Notification);
    }

    [Fact]
    public async Task Review_by_a_role_the_pathway_does_not_allow_is_refused()
    {
        await _harness.OpenCaseAsync();
        var receptionist = new Actor { Reference = "SYN-STAFF-0099", Role = "receptionist", FacilityId = "SYN-FAC-A" };

        var result = await _harness.IngestAsync(Envelopes.ClinicalReview(_harness.Clock.UtcNow, reviewer: receptionist));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
        Assert.Equal(CaseStage.ReviewPending, _harness.TheCase().Stage);
    }

    [Fact]
    public async Task Delivery_report_does_not_complete_the_notification_track()
    {
        await _harness.ReviewedCaseAsync();
        var owner = _harness.TheCase().Owner;

        var @case = await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.Delivered));

        Assert.Equal(NotificationStatus.Delivered, @case.Notification!.Status);
        Assert.False(@case.Notification.IsComplete());
        Assert.Equal("acknowledge-notification", @case.NextAction);
        Assert.Equal(owner, @case.Owner);
        Assert.Single(@case.Notification.Attempts);
    }

    [Fact]
    public async Task All_channels_failed_flags_unreachable_and_sets_a_next_action()
    {
        await _harness.ReviewedCaseAsync();

        var @case = await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.Failed, NotificationChannel.Sms));
        Assert.Equal(NotificationStatus.Failed, @case.Notification!.Status);
        Assert.True(@case.Exceptions.HasFlag(CaseExceptions.Unreachable));
        Assert.Equal("contact-patient-another-way", @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.DueAt);

        await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.Delivered, NotificationChannel.Sms));
        Assert.Equal(NotificationStatus.Delivered, @case.Notification.Status);
        Assert.False(@case.Exceptions.HasFlag(CaseExceptions.Unreachable), "delivery on another channel means the patient was reached");
        Assert.Equal("acknowledge-notification", @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.DueAt);
    }

    [Fact]
    public async Task One_channel_failing_while_another_is_on_its_way_does_not_mark_the_patient_unreachable()
    {
        await _harness.ReviewedCaseAsync();
        Guid sms = Guid.NewGuid(), email = Guid.NewGuid();
        await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.Sent, NotificationChannel.Sms, sms));
        await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.Sent, NotificationChannel.Email, email));

        var @case = await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.Failed, NotificationChannel.Sms, sms));

        Assert.Equal(NotificationStatus.Sent, @case.Notification!.Status);
        Assert.False(@case.Exceptions.HasFlag(CaseExceptions.Unreachable));
        Assert.Equal("acknowledge-notification", @case.NextAction);

        // A late "sent" never moves an attempt back.
        await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.Sent, NotificationChannel.Sms, sms));
        Assert.Equal(NotificationAttemptStatus.Failed, Assert.Single(@case.Notification.Attempts, a => a.Id == sms).Status);
    }

    [Fact]
    public async Task Acknowledgement_completes_the_notification_track()
    {
        await _harness.ReviewedCaseAsync();
        await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.Delivered));
        _harness.Clock.Advance(TimeSpan.FromHours(1));

        var @case = await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.Acknowledged));

        Assert.True(@case.Notification!.IsComplete());
        Assert.Equal(HandlerSupport.ArrangeFollowUpAction, @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.DueAt);
        Assert.Equal(CaseStage.FollowUpActive, @case.Stage);
    }

    [Fact]
    public async Task Call_me_request_raises_CallRequested_for_the_current_owner()
    {
        await _harness.ReviewedCaseAsync();
        await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.Delivered));

        var @case = await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.CallRequested));

        var call = Assert.Single(_harness.Repository.PublishedDomainEvents.OfType<CallRequested>());
        Assert.Equal(@case.Owner, call.CurrentOwner);
        Assert.Equal(@case.DueAt, call.DueAt);
        Assert.Equal(NotificationUpdateHandler.CallPatientAction, @case.NextAction);
        Assert.True(@case.Notification!.CallRequested);
        Assert.False(@case.Notification.IsComplete(), "a call request is not an acknowledgement");
    }

    [Fact]
    public async Task Consultation_owner_proposed_keeps_the_previous_owner_accountable()
    {
        await _harness.ReviewedCaseAsync();
        var owner = _harness.TheCase().Owner;
        var proposed = TestData.Clinician("SYN-STAFF-0002");
        await _harness.ApplyAsync(Envelopes.Consultation(_harness.Clock.UtcNow, ConsultationStatus.Requested, actor: Envelopes.Clinician()));

        var @case = await _harness.ApplyAsync(Envelopes.Consultation(_harness.Clock.UtcNow, ConsultationStatus.OwnerProposed, proposedOwner: proposed, actor: Envelopes.Clinician()));

        Assert.Equal(owner, @case.Owner);
        Assert.Equal(proposed, @case.PendingAssignment!.ProposedOwner);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.PendingAssignment.RespondBy);
        Assert.Equal(ConsultationStatus.OwnerProposed, @case.Consultation!.Status);
        var handoff = Assert.Single(_harness.Repository.PublishedDomainEvents.OfType<HandoffProposed>());
        Assert.Equal(owner, handoff.CurrentOwner);
        Assert.Equal(proposed, handoff.ProposedOwner);
    }

    [Fact]
    public async Task Consultation_accepted_transfers_ownership()
    {
        var proposed = await ProposeConsultationAsync();

        var @case = await _harness.ApplyAsync(Envelopes.Consultation(_harness.Clock.UtcNow, ConsultationStatus.Accepted, actor: Envelopes.Clinician(proposed.Reference)));

        Assert.Equal(proposed, @case.Owner);
        Assert.Null(@case.PendingAssignment);
        Assert.Equal(ConsultationStatus.Accepted, @case.Consultation!.Status);
    }

    [Fact]
    public async Task Consultation_accepted_by_someone_else_is_refused()
    {
        await ProposeConsultationAsync();
        var before = _harness.TheCase().Owner;

        var result = await _harness.IngestAsync(Envelopes.Consultation(_harness.Clock.UtcNow, ConsultationStatus.Accepted, actor: Envelopes.Clinician("SYN-STAFF-0042")));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
        Assert.Equal(before, _harness.TheCase().Owner);
        Assert.NotNull(_harness.TheCase().PendingAssignment);
    }

    [Fact]
    public async Task Consultation_rejected_returns_next_action_to_current_owner()
    {
        var proposed = await ProposeConsultationAsync();
        var owner = _harness.TheCase().Owner;

        var @case = await _harness.ApplyAsync(Envelopes.Consultation(_harness.Clock.UtcNow, ConsultationStatus.Rejected, actor: Envelopes.Clinician(proposed.Reference), reason: "PLACEHOLDER: on leave"));

        Assert.Equal(owner, @case.Owner);
        Assert.Null(@case.PendingAssignment);
        Assert.True(@case.Exceptions.HasFlag(CaseExceptions.Rejected));
        Assert.Equal("reassign-consultation", @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.DueAt);
        var rejected = Assert.Single(_harness.Repository.PublishedDomainEvents.OfType<HandoffRejected>());
        Assert.Equal(owner, rejected.CurrentOwner);
        Assert.Equal("PLACEHOLDER: on leave", rejected.Reason);
    }

    [Fact]
    public async Task Referral_accepted_keeps_the_case_in_follow_up()
    {
        await _harness.ReviewedCaseAsync();
        var nextAction = _harness.TheCase().NextAction;

        var @case = await _harness.ApplyAsync(Envelopes.Referral(_harness.Clock.UtcNow, ReferralStatus.Accepted));

        Assert.Equal(CaseStage.FollowUpActive, @case.Stage);
        Assert.True(@case.Referral!.IsComplete());
        Assert.Null(@case.Closure);
        Assert.Equal(nextAction, @case.NextAction);
    }

    [Fact]
    public async Task Referral_rejected_raises_ReferralRejected_and_sets_a_new_deadline()
    {
        await _harness.ReviewedCaseAsync();
        await _harness.ApplyAsync(Envelopes.Referral(_harness.Clock.UtcNow, ReferralStatus.Sent));
        _harness.Clock.Advance(TimeSpan.FromHours(5));

        var @case = await _harness.ApplyAsync(Envelopes.Referral(_harness.Clock.UtcNow, ReferralStatus.Rejected, reason: "PLACEHOLDER: wrong service"));

        Assert.Equal(ReferralStatus.Rejected, @case.Referral!.Status);
        Assert.Equal("PLACEHOLDER: wrong service", @case.Referral.RejectionReason);
        Assert.True(@case.Exceptions.HasFlag(CaseExceptions.Rejected));
        Assert.Equal("arrange-alternative-referral", @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.DueAt);
        var rejected = Assert.Single(_harness.Repository.PublishedDomainEvents.OfType<ReferralRejected>());
        Assert.Equal(@case.Owner, rejected.CurrentOwner);
        Assert.Equal("PLACEHOLDER: wrong service", rejected.Reason);
    }

    [Fact]
    public async Task Missed_appointment_creates_a_next_action_with_a_deadline()
    {
        await _harness.ReviewedCaseAsync();
        await _harness.ApplyAsync(Envelopes.Appointment(_harness.Clock.UtcNow, AppointmentStatus.Scheduled, scheduledFor: _harness.Clock.UtcNow.AddDays(3)));
        _harness.Clock.Advance(TimeSpan.FromDays(3));

        var @case = await _harness.ApplyAsync(Envelopes.Appointment(_harness.Clock.UtcNow, AppointmentStatus.Missed));

        Assert.Equal(AppointmentStatus.Missed, @case.Appointment!.Status);
        Assert.Equal("rebook-appointment", @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.DueAt);
        Assert.NotNull(@case.Owner);

        _harness.Clock.Advance(TimeSpan.FromHours(2));
        await _harness.ApplyAsync(Envelopes.Appointment(_harness.Clock.UtcNow, AppointmentStatus.Scheduled, scheduledFor: _harness.Clock.UtcNow.AddDays(2)));
        Assert.Equal(AppointmentStatus.Scheduled, @case.Appointment.Status);
        Assert.Equal(HandlerSupport.ArrangeFollowUpAction, @case.NextAction);
    }

    [Fact]
    public async Task Booking_and_rebooking_raise_AppointmentScheduled_with_the_place()
    {
        await _harness.ReviewedCaseAsync();
        var bookedFor = _harness.Clock.UtcNow.AddDays(3);
        await _harness.ApplyAsync(Envelopes.Appointment(_harness.Clock.UtcNow, AppointmentStatus.Scheduled, scheduledFor: bookedFor, location: "SYN Breast Imaging Clinic"));

        var booked = Assert.Single(_harness.Repository.PublishedDomainEvents.OfType<AppointmentScheduled>());
        Assert.Equal(bookedFor, booked.ScheduledFor);
        Assert.Equal("SYN Breast Imaging Clinic", booked.Location);

        _harness.Clock.Advance(TimeSpan.FromDays(3));
        await _harness.ApplyAsync(Envelopes.Appointment(_harness.Clock.UtcNow, AppointmentStatus.Missed));
        Assert.Single(_harness.Repository.PublishedDomainEvents.OfType<AppointmentScheduled>());

        var rebookedFor = _harness.Clock.UtcNow.AddDays(2);
        await _harness.ApplyAsync(Envelopes.Appointment(_harness.Clock.UtcNow, AppointmentStatus.Scheduled, scheduledFor: rebookedFor, location: "SYN Satellite Clinic"));

        var all = _harness.Repository.PublishedDomainEvents.OfType<AppointmentScheduled>().ToList();
        Assert.Equal(2, all.Count);
        Assert.Equal(rebookedFor, all[1].ScheduledFor);
        Assert.Equal("SYN Satellite Clinic", all[1].Location);
    }

    [Fact]
    public async Task A_booking_without_a_time_is_tracked_but_raises_no_AppointmentScheduled()
    {
        await _harness.ReviewedCaseAsync();

        var @case = await _harness.ApplyAsync(Envelopes.Appointment(_harness.Clock.UtcNow, AppointmentStatus.Scheduled, location: "SYN Breast Imaging Clinic"));
        _harness.Clock.Advance(TimeSpan.FromHours(1));
        await _harness.ApplyAsync(Envelopes.Appointment(_harness.Clock.UtcNow, AppointmentStatus.Scheduled));

        Assert.Equal(AppointmentStatus.Scheduled, @case.Appointment!.Status);
        Assert.Empty(_harness.Repository.PublishedDomainEvents.OfType<AppointmentScheduled>());
    }

    [Fact]
    public async Task An_appointment_first_reported_as_attended_raises_no_AppointmentScheduled()
    {
        await _harness.ReviewedCaseAsync();

        await _harness.ApplyAsync(Envelopes.Appointment(_harness.Clock.UtcNow, AppointmentStatus.Attended, scheduledFor: _harness.Clock.UtcNow));

        Assert.Empty(_harness.Repository.PublishedDomainEvents.OfType<AppointmentScheduled>());
    }

    [Fact]
    public async Task Outcome_moves_the_case_to_closure_review()
    {
        await _harness.ReviewedCaseAsync();

        var @case = await _harness.ApplyAsync(Envelopes.Outcome(_harness.Clock.UtcNow, "Procedure/SYN-PROC-0007"));

        Assert.Equal(CaseStage.ClosureReview, @case.Stage);
        Assert.Equal("Procedure/SYN-PROC-0007", @case.OutcomeEvidenceReference);
        Assert.Equal("decide-closure", @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.DueAt);
        Assert.Null(@case.Closure);
        Assert.Equal("Procedure/SYN-PROC-0007", _harness.Repository.Events.Last().EvidenceReference);
    }

    [Fact]
    public async Task Outcome_with_a_kind_the_pathway_does_not_declare_is_refused_and_audited()
    {
        await _harness.ReviewedCaseAsync();
        var events = _harness.Repository.Events.Count;

        var result = await _harness.IngestAsync(Envelopes.Outcome(_harness.Clock.UtcNow, "Procedure/SYN-PROC-0009", evidenceKind: "PLACEHOLDER-UNKNOWN-KIND"));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
        Assert.Contains("PLACEHOLDER-UNKNOWN-KIND", result.Reason);
        var @case = _harness.TheCase();
        Assert.Equal(CaseStage.FollowUpActive, @case.Stage);
        Assert.Null(@case.OutcomeEvidenceReference);
        Assert.Equal(events + 1, _harness.Repository.Events.Count);
        Assert.Equal(SourceEventDisposition.Rejected, _harness.Repository.Events.Last().Disposition);
    }

    [Fact]
    public async Task Treatment_plan_outcome_sets_start_treatment_as_the_next_action()
    {
        await _harness.ReviewedCaseAsync();

        var @case = await _harness.ApplyAsync(Envelopes.Outcome(_harness.Clock.UtcNow, "CarePlan/SYN-PLAN-0001", evidenceKind: "treatment-plan"));

        Assert.Equal(CaseStage.ClosureReview, @case.Stage);
        Assert.Equal("treatment-plan", @case.OutcomeEvidenceKind);
        Assert.Equal("start-treatment", @case.NextAction);
        var startWithin = _harness.Pathway.Deadlines.Single(d => d.NextAction == "start-treatment" && d.UrgencyCode is null).Duration;
        Assert.Equal(_harness.Clock.UtcNow.Add(startWithin), @case.DueAt);
    }

    [Fact]
    public async Task Diagnosis_then_plan_then_treatment_are_recorded_as_separate_outcomes_in_closure_review()
    {
        await _harness.CaseInClosureReviewAsync();
        Assert.Equal("diagnosis", _harness.TheCase().OutcomeEvidenceKind);
        Assert.Equal("decide-closure", _harness.TheCase().NextAction);

        var planned = await _harness.ApplyAsync(Envelopes.Outcome(_harness.Clock.UtcNow, "CarePlan/SYN-PLAN-0001", evidenceKind: "treatment-plan"));
        Assert.Equal(CaseStage.ClosureReview, planned.Stage);
        Assert.Equal("start-treatment", planned.NextAction);
        Assert.Equal("CarePlan/SYN-PLAN-0001", planned.OutcomeEvidenceReference);

        _harness.Clock.Advance(TimeSpan.FromHours(2));
        var started = await _harness.ApplyAsync(Envelopes.Outcome(_harness.Clock.UtcNow, "Procedure/SYN-PROC-0002", evidenceKind: "treatment-started"));
        Assert.Equal(CaseStage.ClosureReview, started.Stage);
        Assert.Equal("treatment-started", started.OutcomeEvidenceKind);
        Assert.Equal("decide-closure", started.NextAction);
        Assert.Null(started.Closure);

        Assert.Equal(3, _harness.Repository.Events.Count(e => e.Type == CaseEventType.OutcomeRecorded && e.Disposition == SourceEventDisposition.Applied));
    }

    [Fact]
    public async Task Further_outcome_in_closure_review_by_a_role_that_may_not_record_outcomes_is_refused()
    {
        await _harness.CaseInClosureReviewAsync();

        var result = await _harness.IngestAsync(Envelopes.Outcome(_harness.Clock.UtcNow, "CarePlan/SYN-PLAN-0001", evidenceKind: "treatment-plan",
            actor: new Actor { Reference = "SYN-STAFF-0099", Role = "receptionist", FacilityId = "SYN-FAC-A" }));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
        Assert.Equal("decide-closure", _harness.TheCase().NextAction);
    }

    [Fact]
    public async Task Closure_with_evidence_closes_the_case()
    {
        await _harness.CaseInClosureReviewAsync();

        var @case = await _harness.ApplyAsync(Envelopes.Closure(_harness.Clock.UtcNow, authorizedBy: Envelopes.Clinician("SYN-STAFF-0002")));

        Assert.Equal(CaseStage.Closed, @case.Stage);
        Assert.Equal(ClosureReason.Treated, @case.Closure!.Reason);
        Assert.Equal("SYN-STAFF-0002", @case.Closure.AuthorizedBy.Reference);
        Assert.Null(@case.NextAction);
        Assert.Null(@case.DueAt);
        Assert.Equal(CaseExceptions.None, @case.Exceptions);
        Assert.Single(_harness.Repository.PublishedDomainEvents.OfType<CaseClosed>());
    }

    [Fact]
    public async Task Closure_without_evidence_is_rejected_and_audited()
    {
        await _harness.CaseInClosureReviewAsync();

        var result = await _harness.IngestAsync(Envelopes.Closure(_harness.Clock.UtcNow, evidenceReference: null));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
        Assert.Contains("evidence", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CaseStage.ClosureReview, _harness.TheCase().Stage);
        var audit = _harness.Repository.AuditEvents.Last();
        Assert.Equal(SourceEventDisposition.Rejected, audit.Disposition);
        Assert.Equal(CaseEventType.ClosureDecided, audit.EventType);
        Assert.Contains("evidence", audit.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Closure_without_an_authorizer_is_rejected_and_audited()
    {
        await _harness.CaseInClosureReviewAsync();

        var result = await _harness.IngestAsync(Envelopes.Closure(_harness.Clock.UtcNow, noAuthorizer: true));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
        Assert.Contains("authorizer", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CaseStage.ClosureReview, _harness.TheCase().Stage);
        var audit = _harness.Repository.AuditEvents.Last();
        Assert.Equal(SourceEventDisposition.Rejected, audit.Disposition);
        Assert.Equal(TestData.SourceSystem, audit.ActorReference);
    }

    [Fact]
    public async Task Closure_before_an_outcome_is_rejected_even_with_an_evidence_reference()
    {
        await _harness.ReviewedCaseAsync();

        var result = await _harness.IngestAsync(Envelopes.Closure(_harness.Clock.UtcNow));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
        Assert.Equal(CaseStage.FollowUpActive, _harness.TheCase().Stage);
        Assert.Null(_harness.TheCase().Closure);
    }

    [Fact]
    public async Task Confidence_rating_is_stored_on_the_track_and_changes_nothing_else()
    {
        await _harness.ReviewedCaseAsync();
        await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.Delivered));
        var before = _harness.TheCase();
        var (stage, owner, nextAction, dueAt) = (before.Stage, before.Owner, before.NextAction, before.DueAt);
        var publishedBefore = _harness.Repository.PublishedDomainEvents.Count;

        var @case = await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.ConfidenceRated, confidence: 4));

        Assert.Equal(4, @case.Notification!.Confidence);
        Assert.Equal(NotificationStatus.Delivered, @case.Notification.Status);
        Assert.False(@case.Notification.IsComplete(), "a confidence rating is not an acknowledgement");
        Assert.Equal((stage, owner, nextAction, dueAt), (@case.Stage, @case.Owner, @case.NextAction, @case.DueAt));
        Assert.Equal(publishedBefore, _harness.Repository.PublishedDomainEvents.Count);
        Assert.Contains("\"confidence\":4", _harness.Repository.AuditEvents.Last().NewProjectionJson);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(null)]
    public async Task Confidence_rating_without_a_value_from_one_to_five_is_refused(int? confidence)
    {
        await _harness.ReviewedCaseAsync();

        var result = await _harness.IngestAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.ConfidenceRated, confidence: confidence));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
        Assert.Null(_harness.TheCase().Notification!.Confidence);
        Assert.Equal(SourceEventDisposition.Rejected, _harness.Repository.Events.Last().Disposition);
    }

    [Fact]
    public async Task Consultation_request_sets_the_answer_deadline_and_the_answer_hands_back_follow_up()
    {
        await _harness.ReviewedCaseAsync();

        var @case = await _harness.ApplyAsync(Envelopes.Consultation(_harness.Clock.UtcNow, ConsultationStatus.Requested, actor: Envelopes.Clinician()));

        Assert.Equal(ConsultationTrack.AnswerAction, @case.NextAction);
        Assert.Equal("answer-consultation", @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.DueAt);

        _harness.Clock.Advance(TimeSpan.FromHours(3));
        await _harness.ApplyAsync(Envelopes.Consultation(_harness.Clock.UtcNow, ConsultationStatus.Accepted, actor: Envelopes.Clinician("SYN-STAFF-0002")));

        Assert.Equal(HandlerSupport.ArrangeFollowUpAction, @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.DueAt);
    }

    [Fact]
    public async Task Repeated_consultation_request_does_not_restart_the_answer_deadline()
    {
        await _harness.ReviewedCaseAsync();
        var @case = await _harness.ApplyAsync(Envelopes.Consultation(_harness.Clock.UtcNow, ConsultationStatus.Requested, actor: Envelopes.Clinician()));
        var dueAt = @case.DueAt;

        _harness.Clock.Advance(TimeSpan.FromHours(20));
        @case = await _harness.ApplyAsync(Envelopes.Consultation(_harness.Clock.UtcNow, ConsultationStatus.Requested, actor: Envelopes.Clinician()));

        Assert.Equal(ConsultationTrack.AnswerAction, @case.NextAction);
        Assert.Equal(dueAt, @case.DueAt);
    }

    [Fact]
    public async Task Repeated_referral_sent_does_not_restart_the_response_deadline()
    {
        await _harness.ReviewedCaseAsync();
        var @case = await _harness.ApplyAsync(Envelopes.Referral(_harness.Clock.UtcNow, ReferralStatus.Sent));
        var dueAt = @case.DueAt;

        _harness.Clock.Advance(TimeSpan.FromHours(3));
        @case = await _harness.ApplyAsync(Envelopes.Referral(_harness.Clock.UtcNow, ReferralStatus.Sent));

        Assert.Equal(ReferralTrack.RespondAction, @case.NextAction);
        Assert.Equal(dueAt, @case.DueAt);
    }

    [Fact]
    public async Task Referral_sent_again_after_a_rejection_restarts_the_response_deadline()
    {
        await _harness.ReviewedCaseAsync();
        await _harness.ApplyAsync(Envelopes.Referral(_harness.Clock.UtcNow, ReferralStatus.Sent));
        _harness.Clock.Advance(TimeSpan.FromHours(1));
        await _harness.ApplyAsync(Envelopes.Referral(_harness.Clock.UtcNow, ReferralStatus.Rejected, reason: "No capacity"));

        _harness.Clock.Advance(TimeSpan.FromHours(2));
        var @case = await _harness.ApplyAsync(Envelopes.Referral(_harness.Clock.UtcNow, ReferralStatus.Sent));

        var respondWithin = _harness.Pathway.Deadlines.Single(d => d.NextAction == ReferralTrack.RespondAction && d.UrgencyCode is null).Duration;
        Assert.Equal(ReferralTrack.RespondAction, @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.Add(respondWithin), @case.DueAt);
    }

    [Fact]
    public async Task Referral_sent_sets_the_response_deadline_and_acceptance_hands_back_follow_up()
    {
        await _harness.ReviewedCaseAsync();

        var @case = await _harness.ApplyAsync(Envelopes.Referral(_harness.Clock.UtcNow, ReferralStatus.Sent));

        Assert.Equal(ReferralTrack.RespondAction, @case.NextAction);
        Assert.Equal("respond-to-referral", @case.NextAction);
        var respondWithin = _harness.Pathway.Deadlines.Single(d => d.NextAction == ReferralTrack.RespondAction && d.UrgencyCode is null).Duration;
        Assert.Equal(_harness.Clock.UtcNow.Add(respondWithin), @case.DueAt);

        _harness.Clock.Advance(TimeSpan.FromHours(1));
        await _harness.ApplyAsync(Envelopes.Referral(_harness.Clock.UtcNow, ReferralStatus.Accepted));

        Assert.True(@case.Referral!.IsComplete());
        Assert.Equal(HandlerSupport.ArrangeFollowUpAction, @case.NextAction);
        Assert.Equal(_harness.Clock.UtcNow.AddDays(1), @case.DueAt);
        Assert.Equal(CaseStage.FollowUpActive, @case.Stage);
    }

    [Fact]
    public async Task Consent_granted_is_recorded_with_evidence_and_audited()
    {
        await _harness.ReviewedCaseAsync();
        var before = _harness.TheCase();
        var (stage, owner, nextAction, dueAt) = (before.Stage, before.Owner, before.NextAction, before.DueAt);

        var @case = await _harness.ApplyAsync(Envelopes.Consent(_harness.Clock.UtcNow, ConsentUpdateKind.Granted));

        var consent = Assert.IsType<Consent>(@case.CaregiverConsent);
        Assert.True(consent.IsActive);
        Assert.Equal(Envelopes.CaregiverContact(), consent.CaregiverContactPoint);
        Assert.Equal("Consent/SYN-CONSENT-0001", consent.EvidenceReference);
        Assert.Equal((stage, owner, nextAction, dueAt), (@case.Stage, @case.Owner, @case.NextAction, @case.DueAt));
        var audit = _harness.Repository.AuditEvents.Last();
        Assert.Equal(CaseEventType.ConsentRecorded, audit.EventType);
        Assert.Equal(SourceEventDisposition.Applied, audit.Disposition);
        Assert.Equal("Consent/SYN-CONSENT-0001", audit.EvidenceReference);
        Assert.Contains("\"isActive\":true", audit.NewProjectionJson);
        Assert.DoesNotContain("SYN-CAREGIVER-INBOX-0001", audit.NewProjectionJson);
    }

    [Fact]
    public async Task Consent_withdrawal_stops_further_caregiver_contact_and_is_audited()
    {
        await _harness.ReviewedCaseAsync();
        await _harness.ApplyAsync(Envelopes.Consent(_harness.Clock.UtcNow, ConsentUpdateKind.Granted));
        _harness.Clock.Advance(TimeSpan.FromDays(1));

        var @case = await _harness.ApplyAsync(Envelopes.Consent(_harness.Clock.UtcNow, ConsentUpdateKind.Withdrawn, consentReference: "Consent/SYN-CONSENT-0001/_history/2"));

        Assert.False(@case.CaregiverConsent!.IsActive);
        Assert.Equal(_harness.Clock.UtcNow, @case.CaregiverConsent.WithdrawnAt);
        Assert.Equal("Consent/SYN-CONSENT-0001/_history/2", @case.CaregiverConsent.WithdrawalEvidenceReference);
        var audit = _harness.Repository.AuditEvents.Last();
        Assert.Contains("\"isActive\":true", audit.PriorProjectionJson);
        Assert.Contains("\"isActive\":false", audit.NewProjectionJson);
    }

    [Fact]
    public async Task Consent_without_a_contact_point_or_while_one_is_active_is_refused()
    {
        await _harness.ReviewedCaseAsync();

        var missing = await _harness.IngestAsync(Envelopes.Consent(_harness.Clock.UtcNow, ConsentUpdateKind.Granted, noContact: true));
        Assert.Equal(IngestOutcome.Rejected, missing.Outcome);
        Assert.Null(_harness.TheCase().CaregiverConsent);

        await _harness.ApplyAsync(Envelopes.Consent(_harness.Clock.UtcNow, ConsentUpdateKind.Granted));
        var second = await _harness.IngestAsync(Envelopes.Consent(_harness.Clock.UtcNow, ConsentUpdateKind.Granted, consentReference: "Consent/SYN-CONSENT-0002"));

        Assert.Equal(IngestOutcome.Rejected, second.Outcome);
        Assert.Equal("Consent/SYN-CONSENT-0001", _harness.TheCase().CaregiverConsent!.EvidenceReference);
    }

    [Fact]
    public async Task Withdrawal_without_an_active_consent_is_refused()
    {
        await _harness.ReviewedCaseAsync();

        var result = await _harness.IngestAsync(Envelopes.Consent(_harness.Clock.UtcNow, ConsentUpdateKind.Withdrawn));

        Assert.Equal(IngestOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task Consent_can_be_withdrawn_but_not_granted_on_a_closed_case()
    {
        await _harness.CaseInClosureReviewAsync();
        await _harness.ApplyAsync(Envelopes.Consent(_harness.Clock.UtcNow, ConsentUpdateKind.Granted));
        await _harness.ApplyAsync(Envelopes.Closure(_harness.Clock.UtcNow));

        var withdrawn = await _harness.ApplyAsync(Envelopes.Consent(_harness.Clock.UtcNow, ConsentUpdateKind.Withdrawn));
        Assert.False(withdrawn.CaregiverConsent!.IsActive);

        var granted = await _harness.IngestAsync(Envelopes.Consent(_harness.Clock.UtcNow, ConsentUpdateKind.Granted, consentReference: "Consent/SYN-CONSENT-0002"));
        Assert.Equal(IngestOutcome.Rejected, granted.Outcome);
    }

    private async Task<CaseOwner> ProposeConsultationAsync()
    {
        await _harness.ReviewedCaseAsync();
        var proposed = TestData.Clinician("SYN-STAFF-0002");
        await _harness.ApplyAsync(Envelopes.Consultation(_harness.Clock.UtcNow, ConsultationStatus.OwnerProposed, proposedOwner: proposed, actor: Envelopes.Clinician()));
        _harness.Clock.Advance(TimeSpan.FromHours(2));
        return proposed;
    }
}
