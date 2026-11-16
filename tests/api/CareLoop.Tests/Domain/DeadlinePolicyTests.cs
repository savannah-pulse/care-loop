using CareLoop.Domain.Cases;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Rules;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>Deadlines and escalation steps come only from the pathway definition.</summary>
public sealed class DeadlinePolicyTests
{
    private readonly FakeClock _clock = new();
    private static PathwayDefinition Pathway => DomainTestData.Pathway;

    [Fact]
    public void Due_time_is_now_plus_the_pathway_duration()
    {
        var deadline = Pathway.Deadlines.Single(d => d.NextAction == "review-result");
        Assert.Equal(_clock.UtcNow + deadline.Duration,
            DeadlinePolicy.ComputeDueAt(deadline.NextAction, null, Pathway, _clock));
    }

    [Theory]
    [InlineData("Infrastructure/Pathways")]
    [InlineData("Infrastructure/Pathways/demo")]
    public void Every_shipped_pathway_defines_the_acknowledgement_deadline_the_worker_flags(string directory)
    {
        var pathway = DomainTestData.Provider(directory).Get("breast-screening");
        Assert.Contains(pathway.Deadlines, d => string.Equals(d.NextAction, DeadlinePolicy.AcknowledgeNotificationAction,
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Urgency_specific_deadline_is_preferred_over_the_general_one()
    {
        var general = Pathway.Deadlines.Single(d => d.NextAction == "acknowledge-notification" && d.UrgencyCode is null);
        var specific = general with { UrgencyCode = "SYN-URGENCY", Duration = TimeSpan.FromMinutes(17), WarnBefore = TimeSpan.FromMinutes(3) };
        var pathway = Pathway with { Deadlines = [general, specific] };
        Assert.Equal(_clock.UtcNow + specific.Duration,
            DeadlinePolicy.ComputeDueAt("ACKNOWLEDGE-NOTIFICATION", "syn-urgency", pathway, _clock));
        Assert.Equal(_clock.UtcNow + general.Duration,
            DeadlinePolicy.ComputeDueAt(general.NextAction, "SYN-OTHER", pathway, _clock));

        var @case = DomainTestData.InFollowUp(_clock);
        @case.RecordReview("SYN-URGENCY", DomainTestData.ClinicianActor(), _clock.UtcNow);
        @case.SetNextAction(general.NextAction, _clock.UtcNow.AddMinutes(4), _clock.UtcNow);
        Assert.Equal(DeadlineStatus.OnTrack, DeadlinePolicy.Classify(@case, pathway, _clock));
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(DeadlineStatus.Approaching, DeadlinePolicy.Classify(@case, pathway, _clock));
    }

    [Fact]
    public void Unknown_next_action_throws()
    {
        Assert.Throws<KeyNotFoundException>(() => DeadlinePolicy.ComputeDueAt("SYN-unknown", null, Pathway, _clock));
        var specificOnly = Pathway with
        {
            Deadlines = [Pathway.Deadlines.First() with { UrgencyCode = "SYN-URGENCY" }],
        };
        Assert.Throws<KeyNotFoundException>(() =>
            DeadlinePolicy.ComputeDueAt(specificOnly.Deadlines[0].NextAction, null, specificOnly, _clock));
    }

    [Theory]
    [InlineData(-1, DeadlineStatus.OnTrack)]
    [InlineData(0, DeadlineStatus.Approaching)]
    [InlineData(1, DeadlineStatus.Approaching)]
    public void Case_inside_the_warning_window_is_approaching(int ticks, DeadlineStatus expected)
    {
        var @case = DomainTestData.InReviewPending(_clock);
        var deadline = DeadlinePolicy.FindDeadline(@case.NextAction!, null, Pathway);
        _clock.Set(@case.DueAt!.Value - deadline.WarnBefore + TimeSpan.FromTicks(ticks));
        Assert.Equal(expected, DeadlinePolicy.Classify(@case, Pathway, _clock));
    }

    [Fact]
    public void Case_past_its_deadline_is_overdue()
    {
        var @case = DomainTestData.InReviewPending(_clock);
        _clock.Set(@case.DueAt!.Value);
        Assert.Equal(DeadlineStatus.Approaching, DeadlinePolicy.Classify(@case, Pathway, _clock));
        Assert.Null(DeadlinePolicy.NextEscalationStep(@case, Pathway, _clock));
        _clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(DeadlineStatus.Overdue, DeadlinePolicy.Classify(@case, Pathway, _clock));
    }

    [Fact]
    public void Closed_case_is_never_overdue()
    {
        var @case = DomainTestData.Closed(_clock);
        _clock.Advance(TimeSpan.FromDays(10));
        Assert.Equal(DeadlineStatus.OnTrack, DeadlinePolicy.Classify(@case, Pathway, _clock));
        Assert.Null(DeadlinePolicy.NextEscalationStep(@case, Pathway, _clock));
    }

    [Fact]
    public void Case_without_a_deadline_is_on_track()
    {
        var @case = TestData.NewCase(_clock.UtcNow);
        Assert.Equal(DeadlineStatus.OnTrack, DeadlinePolicy.Classify(@case, Pathway, _clock));
        Assert.Null(DeadlinePolicy.NextEscalationStep(@case, Pathway, _clock));
    }

    [Fact]
    public void First_escalation_step_goes_to_the_facility_lead()
    {
        var @case = DomainTestData.InReviewPending(_clock);
        _clock.Set(@case.DueAt!.Value.AddTicks(1));
        var step = DeadlinePolicy.NextEscalationStep(@case, Pathway, _clock);
        Assert.Equal("facility-lead", step!.EscalateToRole);
        Assert.Equal(1, step.Level);
    }

    [Fact]
    public void Second_escalation_step_goes_to_the_coordinator()
    {
        var @case = DomainTestData.InReviewPending(_clock);
        var dueAt = @case.DueAt!.Value;
        _clock.Set(dueAt.AddTicks(1));
        Escalate(@case, DeadlinePolicy.NextEscalationStep(@case, Pathway, _clock)!);
        var second = Pathway.EscalationLadder.Single(s => s.Level == 2);
        _clock.Set(dueAt + second.AfterOverdue - TimeSpan.FromTicks(1));
        Assert.Null(DeadlinePolicy.NextEscalationStep(@case, Pathway, _clock));
        _clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(second, DeadlinePolicy.NextEscalationStep(@case, Pathway, _clock));
        Assert.Equal("coordinator", second.EscalateToRole);
    }

    [Fact]
    public void Each_escalation_step_is_raised_only_once_per_deadline()
    {
        var @case = DomainTestData.InReviewPending(_clock);
        _clock.Set(@case.DueAt!.Value.AddDays(2));
        while (DeadlinePolicy.NextEscalationStep(@case, Pathway, _clock) is { } step)
        {
            Escalate(@case, step);
        }

        Assert.Equal(2, @case.Escalations.Count);
        Assert.Null(DeadlinePolicy.NextEscalationStep(@case, Pathway, _clock));
        @case.ResolveEscalations(_clock.UtcNow);
        @case.SetNextAction("review-result", DeadlinePolicy.ComputeDueAt("review-result", null, Pathway, _clock), _clock.UtcNow);
        _clock.Set(@case.DueAt!.Value.AddTicks(1));
        Assert.Equal(1, DeadlinePolicy.NextEscalationStep(@case, Pathway, _clock)!.Level);
    }

    [Fact]
    public void Earlier_open_escalations_do_not_block_a_new_deadline()
    {
        var @case = DomainTestData.InReviewPending(_clock);
        _clock.Set(@case.DueAt!.Value.AddTicks(1));
        Escalate(@case, Pathway.EscalationLadder[0]);
        @case.SetNextAction("call-patient", DeadlinePolicy.ComputeDueAt("call-patient", null, Pathway, _clock), _clock.UtcNow);
        _clock.Set(@case.DueAt!.Value.AddTicks(1));
        Assert.Equal(1, DeadlinePolicy.NextEscalationStep(@case, Pathway, _clock)!.Level);
    }

    [Theory]
    [InlineData("answer-consultation")]
    [InlineData("respond-to-referral")]
    [InlineData("call-patient")]
    [InlineData("start-treatment")]
    public void No_duration_is_hard_coded_in_the_policy(string action)
    {
        var duration = TimeSpan.FromSeconds(73);
        var warnBefore = TimeSpan.FromSeconds(11);
        var pathway = Pathway with
        {
            Deadlines = [new DeadlineDefinition { NextAction = action, Duration = duration, WarnBefore = warnBefore, ClinicallyApproved = false }],
            EscalationLadder = [new EscalationStep { Level = 1, EscalateToRole = "SYN-oversight", AfterOverdue = TimeSpan.FromSeconds(19), ClinicallyApproved = false }],
        };
        var @case = TestData.NewCase(_clock.UtcNow);
        @case.SetNextAction(action, DeadlinePolicy.ComputeDueAt(action, null, pathway, _clock), _clock.UtcNow);
        var dueAt = @case.DueAt!.Value;
        Assert.Equal(_clock.UtcNow + duration, dueAt);
        _clock.Set(dueAt - warnBefore);
        Assert.Equal(DeadlineStatus.Approaching, DeadlinePolicy.Classify(@case, pathway, _clock));
        _clock.Set(dueAt.AddSeconds(18));
        Assert.Null(DeadlinePolicy.NextEscalationStep(@case, pathway, _clock));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("SYN-oversight", DeadlinePolicy.NextEscalationStep(@case, pathway, _clock)!.EscalateToRole);
    }

    private void Escalate(Case @case, EscalationStep step) =>
        @case.Escalate(new Escalation
        {
            Level = step.Level, EscalatedTo = TestData.FacilityLeadQueue() with { Role = step.EscalateToRole },
            RaisedAt = _clock.UtcNow, Reason = "SYN overdue action",
        });
}
