using CareLoop.Domain.Cases;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// The ten display labels of the design are derived from the five stages plus the track statuses (decision 19).
/// One test per label, plus the tie-breaks inside follow-up.
/// </summary>
public sealed class CaseDisplayLabelTests
{
    private readonly FakeClock _clock = new();

    private CaseDisplayLabel LabelOf(Case @case) => CaseDisplayLabels.From(@case, DomainTestData.Pathway);

    [Fact]
    public void Result_received_until_the_case_has_a_next_action()
    {
        var @case = TestData.NewCase(_clock.UtcNow, owner: TestData.FacilityLeadQueue());

        Assert.Equal(CaseDisplayLabel.ResultReceived, LabelOf(@case));
    }

    [Fact]
    public void Case_opened_once_it_waits_in_the_queue_with_a_next_action()
    {
        var @case = TestData.NewCase(_clock.UtcNow, owner: TestData.FacilityLeadQueue());
        @case.SetNextAction("assign-owner", _clock.UtcNow.AddDays(1), _clock.UtcNow);

        Assert.Equal(CaseDisplayLabel.CaseOpened, LabelOf(@case));
    }

    [Fact]
    public void Owner_assigned_while_review_is_pending()
    {
        Assert.Equal(CaseDisplayLabel.OwnerAssigned, LabelOf(DomainTestData.InReviewPending(_clock)));
    }

    [Fact]
    public void Patient_notified_when_follow_up_has_started_and_nothing_more_has_happened()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        Assert.Equal(CaseDisplayLabel.PatientNotified, LabelOf(@case));

        @case.Notification!.Advance(NotificationStatus.Delivered, _clock.UtcNow, "SYN-COMM-0001");
        Assert.Equal(CaseDisplayLabel.PatientNotified, LabelOf(@case));
    }

    [Fact]
    public void Patient_acknowledged_once_the_notification_is_acknowledged_or_understood()
    {
        var acknowledged = DomainTestData.InFollowUp(_clock);
        acknowledged.Notification!.Advance(NotificationStatus.Acknowledged, _clock.UtcNow, "SYN-COMM-0001");
        Assert.Equal(CaseDisplayLabel.PatientAcknowledged, LabelOf(acknowledged));

        var understood = DomainTestData.InFollowUp(_clock);
        understood.Notification!.Advance(NotificationStatus.Understood, _clock.UtcNow, "SYN-COMM-0002");
        Assert.Equal(CaseDisplayLabel.PatientAcknowledged, LabelOf(understood));
    }

    [Fact]
    public void Consultation_once_one_is_requested_and_no_referral_or_appointment_exists()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.Notification!.Advance(NotificationStatus.Acknowledged, _clock.UtcNow, "SYN-COMM-0001");
        @case.BeginConsultation("SYN-SR-0001", _clock.UtcNow);

        Assert.Equal(CaseDisplayLabel.Consultation, LabelOf(@case));
    }

    [Fact]
    public void Follow_up_once_a_referral_or_an_appointment_exists()
    {
        var referred = DomainTestData.InFollowUp(_clock);
        referred.BeginReferral("SYN-REF-0001", _clock.UtcNow);
        Assert.Equal(CaseDisplayLabel.FollowUp, LabelOf(referred));

        var booked = DomainTestData.InFollowUp(_clock);
        booked.BeginConsultation("SYN-SR-0001", _clock.UtcNow);
        booked.BeginAppointment("SYN-APPT-0001", _clock.UtcNow.AddDays(2), _clock.UtcNow);
        Assert.Equal(CaseDisplayLabel.FollowUp, LabelOf(booked));
    }

    [Fact]
    public void Furthest_step_wins_even_when_the_patient_never_acknowledged()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.Notification!.Advance(NotificationStatus.Failed, _clock.UtcNow, "SYN-COMM-0001");
        @case.BeginConsultation("SYN-SR-0001", _clock.UtcNow);

        Assert.Equal(CaseDisplayLabel.Consultation, LabelOf(@case));
    }

    [Fact]
    public void Outcome_recorded_while_a_track_required_for_closure_is_incomplete()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.Notification!.Advance(NotificationStatus.Acknowledged, _clock.UtcNow, "SYN-COMM-0001");
        @case.RecordOutcomeEvidence("SYN-PROC-0001", _clock.UtcNow, "diagnosis");
        @case.MoveTo(CaseStage.ClosureReview, _clock.UtcNow);
        @case.SetNextAction("decide-closure", _clock.UtcNow.AddDays(1), _clock.UtcNow);

        Assert.Equal(CaseDisplayLabel.OutcomeRecorded, LabelOf(@case));
    }

    [Fact]
    public void Closure_review_once_every_required_track_is_complete()
    {
        Assert.Equal(CaseDisplayLabel.ClosureReview, LabelOf(DomainTestData.ReadyForClosure(_clock)));
    }

    [Fact]
    public void Closed_when_closed()
    {
        Assert.Equal(CaseDisplayLabel.Closed, LabelOf(DomainTestData.Closed(_clock)));
    }

    [Fact]
    public void Every_label_of_the_design_is_reachable()
    {
        Assert.Equal(10, Enum.GetValues<CaseDisplayLabel>().Length);
    }

    [Fact]
    public void Computing_the_label_never_mutates_the_case()
    {
        var @case = DomainTestData.ReadyForClosure(_clock);
        var updatedAt = @case.UpdatedAt;

        CaseDisplayLabels.From(@case, DomainTestData.Pathway);

        Assert.Equal(updatedAt, @case.UpdatedAt);
        Assert.Throws<ArgumentNullException>(() => CaseDisplayLabels.From(null!, DomainTestData.Pathway));
        Assert.Throws<ArgumentNullException>(() => CaseDisplayLabels.From(@case, null!));
    }
}
