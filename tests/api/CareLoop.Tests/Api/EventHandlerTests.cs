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
/// Each of the eight handlers applies its event through the Domain rules.
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

        await _harness.ApplyAsync(Envelopes.Notification(_harness.Clock.UtcNow, NotificationUpdateKind.Delivered, NotificationChannel.Simulator));
        Assert.Equal(NotificationStatus.Delivered, @case.Notification.Status);
        Assert.False(@case.Exceptions.HasFlag(CaseExceptions.Unreachable), "delivery on another channel means the patient was reached");
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

    private async Task<CaseOwner> ProposeConsultationAsync()
    {
        await _harness.ReviewedCaseAsync();
        var proposed = TestData.Clinician("SYN-STAFF-0002");
        await _harness.ApplyAsync(Envelopes.Consultation(_harness.Clock.UtcNow, ConsultationStatus.OwnerProposed, proposedOwner: proposed, actor: Envelopes.Clinician()));
        _harness.Clock.Advance(TimeSpan.FromHours(2));
        return proposed;
    }
}
