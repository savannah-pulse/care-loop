using CareLoop.Domain.Cases;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Recipients;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// Invariants of the Case aggregate.
/// </summary>
public sealed class CaseTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void Recording_a_review_stores_the_urgency_exactly_as_received()
    {
        var @case = DomainTestData.InReviewPending(_clock);

        @case.RecordReview("PLACEHOLDER-URGENCY-B", DomainTestData.ClinicianActor(), _clock.UtcNow);

        Assert.Equal("PLACEHOLDER-URGENCY-B", @case.Urgency);
    }

    [Fact]
    public void Closed_case_cannot_move_to_another_stage()
    {
        var @case = DomainTestData.Closed(_clock);

        Assert.Throws<InvalidOperationException>(() => @case.MoveTo(CaseStage.FollowUpActive, _clock.UtcNow));
        Assert.Throws<InvalidOperationException>(() => @case.RecordReview("PLACEHOLDER-URGENCY-A", DomainTestData.ClinicianActor(), _clock.UtcNow));
        Assert.Throws<InvalidOperationException>(() => @case.SetNextAction("review-result", _clock.UtcNow.AddDays(1), _clock.UtcNow));
        Assert.Equal(CaseStage.Closed, @case.Stage);
    }

    [Fact]
    public void Case_cannot_be_moved_to_closed_without_a_closure_decision()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);

        Assert.Throws<InvalidOperationException>(() => @case.MoveTo(CaseStage.Closed, _clock.UtcNow));

        Assert.Equal(CaseStage.ClosureReview, @case.Stage);
        Assert.Null(@case.Closure);
    }

    [Fact]
    public void Exception_always_leaves_a_next_action_an_owner_and_a_deadline()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.FlagException(CaseExceptions.Overdue, _clock.UtcNow);

        @case.FlagException(CaseExceptions.Unreachable, _clock.UtcNow);
        @case.SetNextAction("contact-patient-another-way", _clock.UtcNow.AddDays(1), _clock.UtcNow);

        Assert.True(@case.Exceptions.HasFlag(CaseExceptions.Unreachable));
        Assert.False(@case.Exceptions.HasFlag(CaseExceptions.Overdue), "a new next action starts a new clock");
        Assert.Equal("contact-patient-another-way", @case.NextAction);
        Assert.Equal(_clock.UtcNow.AddDays(1), @case.DueAt);
        Assert.NotNull(@case.Owner);
        Assert.Equal(CaseStage.FollowUpActive, @case.Stage);
    }

    [Fact]
    public void Escalation_is_a_flag_and_does_not_change_stage_or_owner()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        var owner = @case.Owner;
        var stage = @case.Stage;

        @case.Escalate(new Escalation
        {
            Level = 1,
            EscalatedTo = TestData.FacilityLeadQueue(),
            RaisedAt = _clock.UtcNow,
            Reason = "acknowledge-notification overdue",
        });

        Assert.True(@case.IsEscalated);
        Assert.Equal(stage, @case.Stage);
        Assert.Equal(owner, @case.Owner);
        var raised = Assert.Single(@case.DomainEvents.OfType<EscalationRaised>());
        Assert.Equal(1, raised.Level);
        Assert.Equal(owner, raised.CurrentOwner);
        Assert.Equal(TestData.FacilityLeadQueue(), raised.EscalatedTo);
    }

    [Fact]
    public void Resolving_escalations_clears_the_flag()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.Escalate(new Escalation { Level = 1, EscalatedTo = TestData.FacilityLeadQueue(), RaisedAt = _clock.UtcNow, Reason = "overdue" });
        _clock.Advance(TimeSpan.FromHours(3));

        @case.ResolveEscalations(_clock.UtcNow);

        Assert.False(@case.IsEscalated);
        Assert.All(@case.Escalations, e => Assert.Equal(_clock.UtcNow, e.ResolvedAt));
        Assert.Single(@case.Escalations);
    }

    [Fact]
    public void Recording_outcome_evidence_does_not_close_the_case()
    {
        var @case = DomainTestData.InFollowUp(_clock);

        @case.RecordOutcomeEvidence("SYN-PROC-0001", _clock.UtcNow);

        Assert.Equal("SYN-PROC-0001", @case.OutcomeEvidenceReference);
        Assert.NotEqual(CaseStage.Closed, @case.Stage);
        Assert.Null(@case.Closure);
        Assert.Empty(@case.DomainEvents.OfType<CaseClosed>());
    }

    [Fact]
    public void Closing_raises_CaseClosed_and_clears_next_action()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);
        @case.FlagException(CaseExceptions.Overdue, _clock.UtcNow);
        var decision = DomainTestData.TreatedDecision(_clock);

        @case.Close(decision, _clock.UtcNow);

        Assert.Equal(CaseStage.Closed, @case.Stage);
        Assert.Same(decision, @case.Closure);
        Assert.Null(@case.NextAction);
        Assert.Null(@case.DueAt);
        Assert.Equal(CaseExceptions.None, @case.Exceptions);
        var closed = Assert.Single(@case.DomainEvents.OfType<CaseClosed>());
        Assert.Equal(ClosureReason.Treated, closed.Reason);
        Assert.Equal(decision.AuthorizedBy, closed.AuthorizedBy);
    }

    [Fact]
    public void Beginning_notification_raises_NotificationRequested()
    {
        var @case = DomainTestData.InReviewPending(_clock);
        @case.MoveTo(CaseStage.FollowUpActive, _clock.UtcNow);

        var track = @case.BeginNotification("result-notification", _clock.UtcNow);

        Assert.Same(track, @case.Notification);
        var requested = Assert.Single(@case.DomainEvents.OfType<NotificationRequested>());
        Assert.Equal(@case.PatientReference, requested.PatientReference);
        Assert.Equal("result-notification", requested.TemplateKey);
        Assert.Equal(@case.Id, requested.CaseId);
    }

    [Fact]
    public void Each_track_can_be_started_only_once()
    {
        var @case = DomainTestData.InFollowUp(_clock);

        Assert.Throws<InvalidOperationException>(() => @case.BeginNotification("result-notification", _clock.UtcNow));
        @case.BeginReferral("SYN-REF-0001", _clock.UtcNow);
        Assert.Throws<InvalidOperationException>(() => @case.BeginReferral("SYN-REF-0002", _clock.UtcNow));
    }

    [Fact]
    public void Outcome_recorded_before_the_appointment_is_linked_when_the_appointment_starts()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.RecordOutcomeEvidence("SYN-PROC-0001", _clock.UtcNow);

        var appointment = @case.BeginAppointment("SYN-APPT-0001", _clock.UtcNow.AddDays(1), _clock.UtcNow);

        Assert.Equal("SYN-PROC-0001", appointment.OutcomeReference);
    }

    private static ContactPoint CaregiverContact() =>
        new() { Channel = NotificationChannel.Email, Address = "SYN-CAREGIVER-INBOX-0001" };

    [Fact]
    public void Recording_consent_stores_the_caregiver_contact_and_evidence_and_is_active()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        var stage = @case.Stage;
        var owner = @case.Owner;
        var dueAt = @case.DueAt;

        @case.RecordConsent(CaregiverContact(), "Consent/SYN-CONSENT-0001", _clock.UtcNow);

        var consent = Assert.IsType<Consent>(@case.CaregiverConsent);
        Assert.True(consent.IsActive);
        Assert.Equal(CaregiverContact(), consent.CaregiverContactPoint);
        Assert.Equal("Consent/SYN-CONSENT-0001", consent.EvidenceReference);
        Assert.Equal(_clock.UtcNow, consent.GrantedAt);
        Assert.Null(consent.WithdrawnAt);
        Assert.Equal(stage, @case.Stage);
        Assert.Equal(owner, @case.Owner);
        Assert.Equal(dueAt, @case.DueAt);
    }

    [Fact]
    public void Consent_needs_evidence_and_a_contact_point()
    {
        var @case = DomainTestData.InFollowUp(_clock);

        Assert.Throws<ArgumentException>(() => @case.RecordConsent(CaregiverContact(), " ", _clock.UtcNow));
        Assert.Throws<ArgumentNullException>(() => @case.RecordConsent(null!, "Consent/SYN-CONSENT-0001", _clock.UtcNow));
        Assert.Null(@case.CaregiverConsent);
    }

    [Fact]
    public void Withdrawing_consent_keeps_the_record_but_makes_it_inactive()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.RecordConsent(CaregiverContact(), "Consent/SYN-CONSENT-0001", _clock.UtcNow);
        _clock.Advance(TimeSpan.FromDays(2));

        @case.WithdrawConsent("Consent/SYN-CONSENT-0001/_history/2", _clock.UtcNow);

        var consent = @case.CaregiverConsent!;
        Assert.False(consent.IsActive);
        Assert.Equal(_clock.UtcNow, consent.WithdrawnAt);
        Assert.Equal("Consent/SYN-CONSENT-0001/_history/2", consent.WithdrawalEvidenceReference);
        Assert.Equal("Consent/SYN-CONSENT-0001", consent.EvidenceReference);
    }

    [Fact]
    public void Second_consent_while_one_is_active_is_refused_but_allowed_after_withdrawal()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.RecordConsent(CaregiverContact(), "Consent/SYN-CONSENT-0001", _clock.UtcNow);

        Assert.Throws<InvalidOperationException>(() => @case.RecordConsent(CaregiverContact(), "Consent/SYN-CONSENT-0002", _clock.UtcNow));

        @case.WithdrawConsent(null, _clock.UtcNow);
        @case.RecordConsent(CaregiverContact() with { Address = "SYN-CAREGIVER-INBOX-0002" }, "Consent/SYN-CONSENT-0002", _clock.UtcNow);

        Assert.True(@case.CaregiverConsent!.IsActive);
        Assert.Equal("SYN-CAREGIVER-INBOX-0002", @case.CaregiverConsent.CaregiverContactPoint.Address);
    }

    [Fact]
    public void Withdrawal_without_an_active_consent_is_refused()
    {
        var @case = DomainTestData.InFollowUp(_clock);

        Assert.Throws<InvalidOperationException>(() => @case.WithdrawConsent(null, _clock.UtcNow));
    }

    [Fact]
    public void Consent_cannot_be_recorded_on_a_closed_case_but_can_be_withdrawn()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);
        @case.RecordConsent(CaregiverContact(), "Consent/SYN-CONSENT-0001", _clock.UtcNow);
        @case.Close(DomainTestData.TreatedDecision(_clock), _clock.UtcNow);

        Assert.Throws<InvalidOperationException>(() => @case.RecordConsent(CaregiverContact(), "Consent/SYN-CONSENT-0002", _clock.UtcNow));
        @case.WithdrawConsent(null, _clock.UtcNow);

        Assert.False(@case.CaregiverConsent!.IsActive);
    }
}
