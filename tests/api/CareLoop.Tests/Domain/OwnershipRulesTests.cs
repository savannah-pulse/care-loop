using CareLoop.Domain.Cases;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Rules;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// A case always has exactly one accountable owner; transfer is propose-then-accept.
/// </summary>
public sealed class OwnershipRulesTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void New_case_with_a_named_owner_is_owned_by_that_person()
    {
        var named = DomainTestData.ClinicianActor("SYN-STAFF-0007");

        var owner = OwnershipRules.InitialOwner("SYN-FAC-A", named, DomainTestData.Pathway);
        var @case = TestData.NewCase(_clock.UtcNow, owner: owner);

        Assert.Equal("SYN-STAFF-0007", @case.Owner.Reference);
        Assert.Equal("clinician", @case.Owner.Role);
        Assert.False(@case.Owner.IsQueue);
        Assert.False(@case.Exceptions.HasFlag(CaseExceptions.Unassigned));
    }

    [Fact]
    public void New_case_with_no_named_owner_goes_to_the_facility_lead_queue_and_is_flagged_unassigned()
    {
        var owner = OwnershipRules.InitialOwner("SYN-FAC-A", namedOwner: null, DomainTestData.Pathway);
        var @case = TestData.NewCase(_clock.UtcNow, owner: owner);

        Assert.True(@case.Owner.IsQueue);
        Assert.Equal(DomainTestData.Pathway.UnassignedQueueRole, @case.Owner.Role);
        Assert.Equal("SYN-FAC-A", @case.Owner.FacilityId);
        Assert.True(@case.Exceptions.HasFlag(CaseExceptions.Unassigned));
    }

    [Fact]
    public void Proposing_a_new_owner_does_not_change_the_accountable_owner()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        var before = @case.Owner;
        var proposedOwner = DomainTestData.ClinicianOwner("SYN-STAFF-0002");

        var verdict = OwnershipRules.TryPropose(@case, proposedOwner, DomainTestData.ClinicianActor(), DomainTestData.Pathway, _clock, out var proposal);
        @case.ProposeOwner(proposal!);

        Assert.True(verdict.Allowed, verdict.Reason);
        Assert.Equal(before, @case.Owner);
        Assert.Equal(proposedOwner, @case.PendingAssignment!.ProposedOwner);
        Assert.Equal(AssignmentStatus.Proposed, @case.PendingAssignment.Status);
        var raised = Assert.Single(@case.DomainEvents.OfType<HandoffProposed>());
        Assert.Equal(before, raised.CurrentOwner);
        Assert.Equal(proposedOwner, raised.ProposedOwner);
    }

    [Fact]
    public void Accepting_a_proposal_transfers_ownership()
    {
        var @case = Proposed(out var proposedOwner);
        var acceptor = DomainTestData.ClinicianActor(proposedOwner.Reference);

        Assert.True(OwnershipRules.CanRespond(@case, acceptor).Allowed);
        @case.AcceptOwnership(acceptor, _clock.UtcNow);

        Assert.Equal(proposedOwner, @case.Owner);
        Assert.Null(@case.PendingAssignment);
    }

    [Fact]
    public void Accepting_clears_the_unassigned_flag()
    {
        var @case = DomainTestData.InFollowUp(_clock, owner: TestData.FacilityLeadQueue());
        @case.AssignOwner(TestData.FacilityLeadQueue(), _clock.UtcNow);
        var proposedOwner = DomainTestData.ClinicianOwner("SYN-STAFF-0002");
        var facilityLead = new CareLoop.Domain.Events.Actor { Reference = "SYN-STAFF-LEAD", Role = "facility-lead", FacilityId = "SYN-FAC-A" };

        var verdict = OwnershipRules.TryPropose(@case, proposedOwner, facilityLead, DomainTestData.Pathway, _clock, out var proposal);
        Assert.True(verdict.Allowed, verdict.Reason);
        @case.ProposeOwner(proposal!);
        @case.AcceptOwnership(DomainTestData.ClinicianActor(proposedOwner.Reference), _clock.UtcNow);

        Assert.False(@case.Exceptions.HasFlag(CaseExceptions.Unassigned));
        Assert.False(@case.Owner.IsQueue);
    }

    [Fact]
    public void Rejecting_a_proposal_returns_the_next_action_to_the_current_owner()
    {
        var @case = Proposed(out var proposedOwner);
        var owner = @case.Owner;
        var nextAction = @case.NextAction;

        @case.RejectOwnership(DomainTestData.ClinicianActor(proposedOwner.Reference), "PLACEHOLDER: on leave", _clock.UtcNow);

        Assert.Equal(owner, @case.Owner);
        Assert.Equal(nextAction, @case.NextAction);
        Assert.Null(@case.PendingAssignment);
    }

    [Fact]
    public void Rejected_handoff_raises_HandoffRejected_and_flags_the_exception()
    {
        var @case = Proposed(out var proposedOwner);

        @case.RejectOwnership(DomainTestData.ClinicianActor(proposedOwner.Reference), "PLACEHOLDER: on leave", _clock.UtcNow);

        Assert.True(@case.Exceptions.HasFlag(CaseExceptions.Rejected));
        var rejected = Assert.Single(@case.DomainEvents.OfType<HandoffRejected>());
        Assert.Equal(@case.Owner, rejected.CurrentOwner);
        Assert.Equal(proposedOwner, rejected.RejectedBy);
        Assert.Equal("PLACEHOLDER: on leave", rejected.Reason);
    }

    [Fact]
    public void Only_the_proposed_owner_can_accept_or_reject()
    {
        var @case = Proposed(out _);
        var somebodyElse = DomainTestData.ClinicianActor("SYN-STAFF-0042");

        var verdict = OwnershipRules.CanRespond(@case, somebodyElse);

        Assert.False(verdict.Allowed);
        Assert.NotNull(verdict.Reason);
        Assert.Throws<InvalidOperationException>(() => @case.AcceptOwnership(somebodyElse, _clock.UtcNow));
        Assert.Throws<InvalidOperationException>(() => @case.RejectOwnership(somebodyElse, "no", _clock.UtcNow));
        Assert.NotNull(@case.PendingAssignment);
    }

    [Fact]
    public void Response_without_a_pending_proposal_is_refused()
    {
        var @case = DomainTestData.InFollowUp(_clock);

        Assert.False(OwnershipRules.CanRespond(@case, DomainTestData.ClinicianActor()).Allowed);
        Assert.Throws<InvalidOperationException>(() => @case.AcceptOwnership(DomainTestData.ClinicianActor(), _clock.UtcNow));
    }

    [Fact]
    public void Second_proposal_while_one_is_pending_is_refused()
    {
        var @case = Proposed(out _);

        var verdict = OwnershipRules.TryPropose(@case, DomainTestData.ClinicianOwner("SYN-STAFF-0003"), DomainTestData.ClinicianActor(), DomainTestData.Pathway, _clock, out var proposal);

        Assert.False(verdict.Allowed);
        Assert.Null(proposal);
        Assert.Throws<InvalidOperationException>(() => @case.ProposeOwner(new Assignment
        {
            ProposedOwner = DomainTestData.ClinicianOwner("SYN-STAFF-0003"),
            ProposedBy = DomainTestData.ClinicianActor(),
            ProposedAt = _clock.UtcNow,
            RespondBy = _clock.UtcNow.AddDays(1),
            Status = AssignmentStatus.Proposed,
        }));
    }

    [Fact]
    public void Proposal_on_a_closed_case_is_refused()
    {
        var @case = DomainTestData.Closed(_clock);

        var verdict = OwnershipRules.TryPropose(@case, DomainTestData.ClinicianOwner(), DomainTestData.ClinicianActor(), DomainTestData.Pathway, _clock, out var proposal);

        Assert.False(verdict.Allowed);
        Assert.Null(proposal);
    }

    [Fact]
    public void Proposal_by_someone_who_may_not_hand_over_the_case_is_refused()
    {
        var @case = DomainTestData.InFollowUp(_clock);

        var verdict = OwnershipRules.TryPropose(@case, DomainTestData.ClinicianOwner(), DomainTestData.ReceptionistActor(), DomainTestData.Pathway, _clock, out var proposal);

        Assert.False(verdict.Allowed);
        Assert.Null(proposal);
    }

    [Fact]
    public void Proposal_to_a_queue_is_refused()
    {
        var @case = DomainTestData.InFollowUp(_clock);

        var verdict = OwnershipRules.TryPropose(@case, TestData.FacilityLeadQueue(), DomainTestData.ClinicianActor(), DomainTestData.Pathway, _clock, out _);

        Assert.False(verdict.Allowed);
    }

    [Fact]
    public void Proposal_carries_a_response_deadline_from_the_pathway()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        var expected = DomainTestData.Pathway.Deadlines
            .Single(d => d.NextAction == OwnershipRules.RespondToHandoffAction && d.UrgencyCode is null)
            .Duration;

        OwnershipRules.TryPropose(@case, DomainTestData.ClinicianOwner(), DomainTestData.ClinicianActor(), DomainTestData.Pathway, _clock, out var proposal);

        Assert.NotNull(proposal);
        Assert.Equal(_clock.UtcNow, proposal.ProposedAt);
        Assert.Equal(_clock.UtcNow.Add(expected), proposal.RespondBy);
    }

    [Fact]
    public void Pathway_without_a_handoff_deadline_refuses_the_proposal()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        var pathway = DomainTestData.Pathway with
        {
            Deadlines = DomainTestData.Pathway.Deadlines.Where(d => d.NextAction != OwnershipRules.RespondToHandoffAction).ToList(),
        };

        var verdict = OwnershipRules.TryPropose(@case, DomainTestData.ClinicianOwner(), DomainTestData.ClinicianActor(), pathway, _clock, out var proposal);

        Assert.False(verdict.Allowed);
        Assert.Null(proposal);
        Assert.Contains(OwnershipRules.RespondToHandoffAction, verdict.Reason);
    }

    private Case Proposed(out CaseOwner proposedOwner)
    {
        var @case = DomainTestData.InFollowUp(_clock);
        proposedOwner = DomainTestData.ClinicianOwner("SYN-STAFF-0002");
        var verdict = OwnershipRules.TryPropose(@case, proposedOwner, DomainTestData.ClinicianActor(), DomainTestData.Pathway, _clock, out var proposal);
        Assert.True(verdict.Allowed, verdict.Reason);
        @case.ProposeOwner(proposal!);
        @case.ClearDomainEvents();
        return @case;
    }
}
