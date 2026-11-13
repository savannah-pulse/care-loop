using CareLoop.Domain.Cases;
using CareLoop.Domain.Rules;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// Closure needs an authorized human decision with evidence or a documented exception.
/// </summary>
public sealed class ClosureGateTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void Closure_with_evidence_by_an_authorized_role_is_allowed()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);

        var verdict = ClosureGate.Evaluate(@case, DomainTestData.TreatedDecision(_clock), DomainTestData.Pathway);

        Assert.True(verdict.Allowed, verdict.Reason);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void Closure_without_evidence_is_rejected()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);
        var decision = DomainTestData.TreatedDecision(_clock) with { EvidenceReference = null };

        var verdict = ClosureGate.Evaluate(@case, decision, DomainTestData.Pathway);

        Assert.False(verdict.Allowed);
        Assert.Contains("evidence", verdict.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Closure_by_an_unauthorized_role_is_rejected()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);
        var decision = DomainTestData.TreatedDecision(_clock, authorizedBy: DomainTestData.ReceptionistActor());

        var verdict = ClosureGate.Evaluate(@case, decision, DomainTestData.Pathway);

        Assert.False(verdict.Allowed);
        Assert.Contains("receptionist", verdict.Reason);
    }

    [Fact]
    public void Closure_by_the_system_role_is_rejected()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);
        var decision = DomainTestData.TreatedDecision(_clock, authorizedBy: DomainTestData.SystemActor());
        var permissivePathway = DomainTestData.Pathway with
        {
            ClosureRules = DomainTestData.Pathway.ClosureRules.Select(r => r with { AuthorizedRoles = [.. r.AuthorizedRoles, "system"] }).ToList(),
        };

        Assert.False(ClosureGate.Evaluate(@case, decision, DomainTestData.Pathway).Allowed);
        Assert.False(ClosureGate.Evaluate(@case, decision, permissivePathway).Allowed, "even a pathway listing the system role must not let automation close a case");
    }

    [Fact]
    public void Premature_closure_before_any_outcome_is_rejected()
    {
        var @case = DomainTestData.InFollowUp(_clock);

        var verdict = ClosureGate.Evaluate(@case, DomainTestData.TreatedDecision(_clock), DomainTestData.Pathway);

        Assert.False(verdict.Allowed);
        Assert.Contains("outcome", verdict.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Closure_with_a_required_track_incomplete_is_rejected()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.RecordOutcomeEvidence("SYN-PROC-0001", _clock.UtcNow);
        @case.MoveTo(CaseStage.ClosureReview, _clock.UtcNow);

        var verdict = ClosureGate.Evaluate(@case, DomainTestData.TreatedDecision(_clock), DomainTestData.Pathway);

        Assert.False(verdict.Allowed);
        Assert.Contains("Notification", verdict.Reason);
        Assert.Contains("Appointment", verdict.Reason);
    }

    [Fact]
    public void Sent_referral_is_not_accepted_as_evidence_of_completed_care()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);
        @case.BeginReferral("SYN-REF-0001", _clock.UtcNow);

        var verdict = ClosureGate.Evaluate(@case, DomainTestData.TreatedDecision(_clock, evidenceReference: "SYN-REF-0001"), DomainTestData.Pathway);

        Assert.False(verdict.Allowed);
        Assert.Contains("referral", verdict.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Accepted_referral_is_not_accepted_as_evidence_of_completed_care()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);
        @case.BeginReferral("SYN-REF-0001", _clock.UtcNow).Advance(ReferralStatus.Accepted, _clock.UtcNow, null);

        var verdict = ClosureGate.Evaluate(@case, DomainTestData.TreatedDecision(_clock, evidenceReference: "SYN-REF-0001"), DomainTestData.Pathway);

        Assert.False(verdict.Allowed);
        Assert.Contains("referral", verdict.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Surveillance_closure_without_a_next_date_is_rejected()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);
        var decision = DomainTestData.TreatedDecision(_clock) with { Reason = ClosureReason.OnSurveillance };

        var rejected = ClosureGate.Evaluate(@case, decision, DomainTestData.Pathway);
        var allowed = ClosureGate.Evaluate(@case, decision with { NextSurveillanceDate = new DateOnly(2027, 1, 1) }, DomainTestData.Pathway);

        Assert.False(rejected.Allowed);
        Assert.Contains("surveillance", rejected.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(allowed.Allowed, allowed.Reason);
    }

    [Fact]
    public void Declined_closure_without_an_exception_note_is_rejected()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        var decision = new ClosureDecision
        {
            Reason = ClosureReason.Declined,
            AuthorizedBy = DomainTestData.ClinicianActor(),
            DecidedAt = _clock.UtcNow,
        };

        var rejected = ClosureGate.Evaluate(@case, decision, DomainTestData.Pathway);
        var allowed = ClosureGate.Evaluate(@case, decision with { ExceptionNote = "PLACEHOLDER: patient declined further follow-up" }, DomainTestData.Pathway);

        Assert.False(rejected.Allowed);
        Assert.Contains("note", rejected.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(allowed.Allowed, allowed.Reason);
    }

    [Fact]
    public void Transferred_closure_requires_acceptance_elsewhere()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        var referral = @case.BeginReferral("SYN-REF-0001", _clock.UtcNow);
        var decision = new ClosureDecision
        {
            Reason = ClosureReason.TransferredAndAccepted,
            AuthorizedBy = DomainTestData.ClinicianActor(),
            DecidedAt = _clock.UtcNow,
            EvidenceReference = "SYN-TASK-ACCEPT-0001",
            ExceptionNote = "PLACEHOLDER: care transferred to facility B",
        };

        var whileSent = ClosureGate.Evaluate(@case, decision, DomainTestData.Pathway);
        referral.Advance(ReferralStatus.Accepted, _clock.UtcNow, "SYN-TASK-ACCEPT-0001");
        var onceAccepted = ClosureGate.Evaluate(@case, decision, DomainTestData.Pathway);

        Assert.False(whileSent.Allowed);
        Assert.Contains("accepted", whileSent.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(onceAccepted.Allowed, onceAccepted.Reason);
    }

    [Fact]
    public void Untraceable_closure_without_documented_attempts_is_rejected()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        var decision = new ClosureDecision
        {
            Reason = ClosureReason.Untraceable,
            AuthorizedBy = DomainTestData.ClinicianActor(),
            DecidedAt = _clock.UtcNow,
            ExceptionNote = "PLACEHOLDER: no contact after documented attempts",
        };

        var rejected = ClosureGate.Evaluate(@case, decision, DomainTestData.Pathway);
        var allowed = ClosureGate.Evaluate(@case, decision with { DocumentedAttemptReferences = ["SYN-COMM-0001"] }, DomainTestData.Pathway);

        Assert.False(rejected.Allowed);
        Assert.Contains("attempt", rejected.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(allowed.Allowed, allowed.Reason);
    }

    [Fact]
    public void Closing_an_already_closed_case_is_rejected()
    {
        var @case = DomainTestData.Closed(_clock);

        var verdict = ClosureGate.Evaluate(@case, DomainTestData.TreatedDecision(_clock), DomainTestData.Pathway);

        Assert.False(verdict.Allowed);
        Assert.Contains("closed", verdict.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => @case.Close(DomainTestData.TreatedDecision(_clock), _clock.UtcNow));
    }

    [Fact]
    public void Reason_without_a_rule_in_the_pathway_is_rejected()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);
        var pathway = DomainTestData.Pathway with
        {
            ClosureRules = DomainTestData.Pathway.ClosureRules.Where(r => r.Reason != ClosureReason.Treated).ToList(),
        };

        var verdict = ClosureGate.Evaluate(@case, DomainTestData.TreatedDecision(_clock), pathway);

        Assert.False(verdict.Allowed);
        Assert.Contains("Treated", verdict.Reason);
    }

    [Fact]
    public void Rejection_states_the_reason_for_the_audit_trail()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);
        var decision = DomainTestData.TreatedDecision(_clock) with { EvidenceReference = null };

        var verdict = ClosureGate.Evaluate(@case, decision, DomainTestData.Pathway);

        Assert.False(verdict.Allowed);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Reason));
        Assert.Contains("Treated", verdict.Reason);
    }

    [Fact]
    public void Evaluate_never_mutates_the_case()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);
        var updatedAt = @case.UpdatedAt;

        ClosureGate.Evaluate(@case, DomainTestData.TreatedDecision(_clock), DomainTestData.Pathway);

        Assert.Equal(CaseStage.ClosureReview, @case.Stage);
        Assert.Null(@case.Closure);
        Assert.Equal(updatedAt, @case.UpdatedAt);
        Assert.Empty(@case.DomainEvents);
    }
}
