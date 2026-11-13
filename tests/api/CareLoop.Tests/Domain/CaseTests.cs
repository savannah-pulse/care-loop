using CareLoop.Domain.Cases;
using CareLoop.Domain.Events.DomainEvents;
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
}
