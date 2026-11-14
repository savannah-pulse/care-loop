using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Rules;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// Acknowledging an escalation (C15) records who in the oversight role has seen it and nothing more.
/// </summary>
public sealed class EscalationAcknowledgementTests
{
    private readonly FakeClock _clock = new();

    private static Actor FacilityLead(string id = "SYN-LEAD-0001") =>
        new() { Reference = id, Role = "facility-lead", FacilityId = "SYN-FAC-A" };

    private static Actor Coordinator(string id = "SYN-COORD-0001") =>
        new() { Reference = id, Role = "coordinator", FacilityId = "SYN-FAC-A" };

    private Case EscalatedCase(int level = 1)
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.FlagException(CaseExceptions.Overdue, _clock.UtcNow);
        @case.Escalate(new Escalation
        {
            Level = level,
            EscalatedTo = level == 1
                ? TestData.FacilityLeadQueue()
                : new CaseOwner { Reference = "SYN-QUEUE-COORD", Role = "coordinator", FacilityId = "SYN-FAC-A", IsQueue = true },
            RaisedAt = _clock.UtcNow,
            Reason = "acknowledge-notification overdue",
        });
        @case.ClearDomainEvents();
        return @case;
    }

    [Fact]
    public void Escalated_to_role_can_acknowledge_and_it_is_recorded()
    {
        var @case = EscalatedCase();
        _clock.Advance(TimeSpan.FromMinutes(30));

        @case.AcknowledgeEscalation(FacilityLead(), _clock.UtcNow);

        var escalation = Assert.Single(@case.Escalations);
        Assert.True(escalation.IsAcknowledged);
        Assert.Equal(FacilityLead(), escalation.AcknowledgedBy);
        Assert.Equal(_clock.UtcNow, escalation.AcknowledgedAt);
        Assert.Null(escalation.ResolvedAt);
        Assert.True(@case.IsEscalated, "acknowledging does not resolve the escalation");
        Assert.Equal(_clock.UtcNow, @case.UpdatedAt);
    }

    [Fact]
    public void Acknowledging_never_clears_exceptions_changes_the_owner_or_moves_the_deadline()
    {
        var @case = EscalatedCase();
        var owner = @case.Owner;
        var nextAction = @case.NextAction;
        var dueAt = @case.DueAt;
        var exceptions = @case.Exceptions;
        var stage = @case.Stage;
        _clock.Advance(TimeSpan.FromHours(1));

        @case.AcknowledgeEscalation(FacilityLead(), _clock.UtcNow);

        Assert.Equal(owner, @case.Owner);
        Assert.Equal(nextAction, @case.NextAction);
        Assert.Equal(dueAt, @case.DueAt);
        Assert.Equal(exceptions, @case.Exceptions);
        Assert.True(@case.Exceptions.HasFlag(CaseExceptions.Overdue));
        Assert.Equal(stage, @case.Stage);
        Assert.Null(@case.PendingAssignment);
        Assert.Empty(@case.DomainEvents);
    }

    [Fact]
    public void Another_role_cannot_acknowledge()
    {
        var @case = EscalatedCase();

        var ex = Assert.Throws<InvalidOperationException>(() => @case.AcknowledgeEscalation(Coordinator(), _clock.UtcNow));

        Assert.Contains("facility-lead", ex.Message);
        Assert.False(Assert.Single(@case.Escalations).IsAcknowledged);
        Assert.Throws<InvalidOperationException>(() => @case.AcknowledgeEscalation(DomainTestData.ClinicianActor(), _clock.UtcNow));
    }

    [Fact]
    public void Nothing_to_acknowledge_without_an_open_escalation()
    {
        var never = DomainTestData.InFollowUp(_clock);
        Assert.Throws<InvalidOperationException>(() => never.AcknowledgeEscalation(FacilityLead(), _clock.UtcNow));

        var resolved = EscalatedCase();
        resolved.ResolveEscalations(_clock.UtcNow);
        Assert.Throws<InvalidOperationException>(() => resolved.AcknowledgeEscalation(FacilityLead(), _clock.UtcNow));
    }

    [Fact]
    public void Acknowledgement_is_recorded_once_per_escalation()
    {
        var @case = EscalatedCase();
        @case.AcknowledgeEscalation(FacilityLead(), _clock.UtcNow);
        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Throws<InvalidOperationException>(() => @case.AcknowledgeEscalation(FacilityLead("SYN-LEAD-0002"), _clock.UtcNow));

        Assert.Equal("SYN-LEAD-0001", Assert.Single(@case.Escalations).AcknowledgedBy!.Reference);
    }

    [Fact]
    public void The_current_escalation_is_the_latest_open_one()
    {
        var @case = EscalatedCase();
        _clock.Advance(TimeSpan.FromDays(1));
        @case.Escalate(new Escalation
        {
            Level = 2,
            EscalatedTo = new CaseOwner { Reference = "SYN-QUEUE-COORD", Role = "coordinator", FacilityId = "SYN-FAC-A", IsQueue = true },
            RaisedAt = _clock.UtcNow,
            Reason = "still overdue",
        });

        Assert.Throws<InvalidOperationException>(() => @case.AcknowledgeEscalation(FacilityLead(), _clock.UtcNow));
        @case.AcknowledgeEscalation(Coordinator(), _clock.UtcNow);

        Assert.False(@case.Escalations[0].IsAcknowledged);
        Assert.True(@case.Escalations[1].IsAcknowledged);
    }

    [Fact]
    public void Closed_case_refuses_acknowledgement()
    {
        var @case = DomainTestData.Closed(_clock);

        Assert.Throws<InvalidOperationException>(() => @case.AcknowledgeEscalation(FacilityLead(), _clock.UtcNow));
    }

    [Fact]
    public void Acknowledgement_event_is_allowed_on_any_open_case_without_changing_the_stage()
    {
        var @case = EscalatedCase();

        var result = TransitionRules.Evaluate(@case, CaseEventType.EscalationAcknowledged, FacilityLead(), DomainTestData.Pathway);

        Assert.True(result.Verdict.Allowed);
        Assert.Null(result.NextStage);
        Assert.Null(result.NextAction);
        Assert.False(TransitionRules.Evaluate(DomainTestData.Closed(_clock), CaseEventType.EscalationAcknowledged, FacilityLead(), DomainTestData.Pathway).Verdict.Allowed);
    }
    [Fact]
    public void Next_level_says_who_acknowledged_the_previous_level_and_when()
    {
        var @case = EscalatedCase();
        _clock.Advance(TimeSpan.FromMinutes(30));
        var acknowledgedAt = _clock.UtcNow;
        @case.AcknowledgeEscalation(FacilityLead(), acknowledgedAt);
        _clock.Advance(TimeSpan.FromHours(4));

        @case.Escalate(Level2());

        var raised = Assert.Single(@case.DomainEvents.OfType<EscalationRaised>());
        Assert.Equal(2, raised.Level);
        Assert.Equal("facility-lead", raised.AcknowledgedByRole);
        Assert.Equal(acknowledgedAt, raised.AcknowledgedAt);
    }

    [Fact]
    public void Next_level_carries_no_acknowledgement_when_nobody_acknowledged_the_previous_level()
    {
        var @case = EscalatedCase();
        _clock.Advance(TimeSpan.FromHours(4));

        @case.Escalate(Level2());

        var raised = Assert.Single(@case.DomainEvents.OfType<EscalationRaised>());
        Assert.Null(raised.AcknowledgedByRole);
        Assert.Null(raised.AcknowledgedAt);
    }

    [Fact]
    public void First_level_carries_no_acknowledgement()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.ClearDomainEvents();

        @case.Escalate(new Escalation
        {
            Level = 1,
            EscalatedTo = TestData.FacilityLeadQueue(),
            RaisedAt = _clock.UtcNow,
            Reason = "acknowledge-notification overdue",
        });

        var raised = Assert.Single(@case.DomainEvents.OfType<EscalationRaised>());
        Assert.Null(raised.AcknowledgedByRole);
        Assert.Null(raised.AcknowledgedAt);
    }

    [Fact]
    public void An_acknowledgement_on_a_resolved_earlier_escalation_is_not_carried_forward()
    {
        var @case = EscalatedCase();
        @case.AcknowledgeEscalation(FacilityLead(), _clock.UtcNow);
        @case.ResolveEscalations(_clock.UtcNow);
        _clock.Advance(TimeSpan.FromDays(1));

        @case.Escalate(Level2());

        var raised = Assert.Single(@case.DomainEvents.OfType<EscalationRaised>());
        Assert.Null(raised.AcknowledgedByRole);
        Assert.Null(raised.AcknowledgedAt);
    }

    [Fact]
    public void Escalate_gives_each_escalation_an_id_and_the_event_names_it()
    {
        var @case = EscalatedCase();
        _clock.Advance(TimeSpan.FromHours(4));

        @case.Escalate(Level2());

        Assert.All(@case.Escalations, e => Assert.NotEqual(Guid.Empty, e.Id));
        Assert.NotEqual(@case.Escalations[0].Id, @case.Escalations[1].Id);
        Assert.Equal(@case.Escalations[1].Id, Assert.Single(@case.DomainEvents.OfType<EscalationRaised>()).EscalationId);
    }

    [Fact]
    public void A_given_id_is_kept_and_a_duplicate_is_refused()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        var id = Guid.NewGuid();
        var escalation = new Escalation { Id = id, Level = 1, EscalatedTo = TestData.FacilityLeadQueue(), RaisedAt = _clock.UtcNow, Reason = "acknowledge-notification overdue" };

        @case.Escalate(escalation);

        Assert.Equal(id, Assert.Single(@case.Escalations).Id);
        Assert.Throws<InvalidOperationException>(() => @case.Escalate(escalation));
    }

    [Fact]
    public void Acknowledging_by_id_records_the_named_escalation()
    {
        var @case = EscalatedCase();
        var id = Assert.Single(@case.Escalations).Id;

        @case.AcknowledgeEscalation(id, FacilityLead(), _clock.UtcNow);

        Assert.True(Assert.Single(@case.Escalations).IsAcknowledged);
    }

    [Fact]
    public void Acknowledging_a_superseded_or_unknown_escalation_is_refused()
    {
        var @case = EscalatedCase();
        var levelOne = Assert.Single(@case.Escalations).Id;
        @case.Escalate(Level2());

        Assert.Throws<InvalidOperationException>(() => @case.AcknowledgeEscalation(levelOne, Coordinator(), _clock.UtcNow));
        Assert.Throws<InvalidOperationException>(() => @case.AcknowledgeEscalation(Guid.NewGuid(), Coordinator(), _clock.UtcNow));
        Assert.All(@case.Escalations, e => Assert.False(e.IsAcknowledged));
    }

    [Fact]
    public void Acknowledging_a_resolved_escalation_by_id_is_refused()
    {
        var @case = EscalatedCase();
        var id = Assert.Single(@case.Escalations).Id;
        @case.ResolveEscalations(_clock.UtcNow);

        Assert.Throws<InvalidOperationException>(() => @case.AcknowledgeEscalation(id, FacilityLead(), _clock.UtcNow));
    }

    private Escalation Level2() => new()
    {
        Level = 2,
        EscalatedTo = new CaseOwner { Reference = "SYN-QUEUE-COORD", Role = "coordinator", FacilityId = "SYN-FAC-A", IsQueue = true },
        RaisedAt = _clock.UtcNow,
        Reason = "acknowledge-notification overdue",
    };
}
