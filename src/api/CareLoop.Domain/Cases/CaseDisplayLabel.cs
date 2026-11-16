using CareLoop.Domain.Pathways;
using CareLoop.Domain.Tracks;

namespace CareLoop.Domain.Cases;

/// <summary>
/// The ten labels the screen designs show for where a case is, computed from the five
/// <see cref="CaseStage"/> values plus the track statuses (decisions 1 and 19).
/// </summary>
/// <remarks>
/// The Domain keeps five stages because that is what the transition rules guard. The design shows
/// ten steps because that is what a person following a case wants to see. The label is derived,
/// never stored, so the two can never disagree: the API computes it on reads and in metrics, and
/// the web app shows what it is given. The members are ordered as the design's flow lists them.
/// </remarks>
public enum CaseDisplayLabel
{
    /// <summary>The result has been ingested and nothing has been asked of anyone yet.</summary>
    ResultReceived = 1,

    /// <summary>The case exists with its first next action, waiting in the queue for an owner.</summary>
    CaseOpened = 2,

    /// <summary>A named owner holds the case and the result awaits clinical review.</summary>
    OwnerAssigned = 3,

    /// <summary>Follow-up has started and the patient has been sent the notification.</summary>
    PatientNotified = 4,

    /// <summary>The patient acknowledged the notification (or confirmed understanding).</summary>
    PatientAcknowledged = 5,

    /// <summary>A consultation has been requested and no referral or appointment exists yet.</summary>
    Consultation = 6,

    /// <summary>A referral has been sent or an appointment booked: follow-up care is being arranged.</summary>
    FollowUp = 7,

    /// <summary>Outcome evidence is recorded but a track the pathway requires for closure is still incomplete.</summary>
    OutcomeRecorded = 8,

    /// <summary>Outcome evidence is recorded and every required track is complete, so an authorized closure decision is possible.</summary>
    ClosureReview = 9,

    /// <summary>The case is closed.</summary>
    Closed = 10,
}

/// <summary>
/// Computes the <see cref="CaseDisplayLabel"/> of a case.
/// </summary>
public static class CaseDisplayLabels
{
    /// <summary>
    /// Works out which of the ten display labels describes a case right now.
    /// </summary>
    /// <param name="case">The case.</param>
    /// <param name="pathway">The pathway the case follows, for which tracks closure requires.</param>
    /// <returns>The label.</returns>
    /// <remarks>
    /// <para>
    /// Labels 1 to 3 and 8 to 10 follow the stage. In <see cref="CaseStage.Received"/> the case is
    /// <see cref="CaseDisplayLabel.ResultReceived"/> until it has a next action and
    /// <see cref="CaseDisplayLabel.CaseOpened"/> from then on; <see cref="CaseStage.ReviewPending"/>
    /// is <see cref="CaseDisplayLabel.OwnerAssigned"/>; <see cref="CaseStage.ClosureReview"/> is
    /// <see cref="CaseDisplayLabel.ClosureReview"/> once every track the pathway marks
    /// <see cref="TrackDefinition.RequiredForClosure"/> is complete and
    /// <see cref="CaseDisplayLabel.OutcomeRecorded"/> while one is not; <see cref="CaseStage.Closed"/>
    /// is <see cref="CaseDisplayLabel.Closed"/>.
    /// </para>
    /// <para>
    /// In <see cref="CaseStage.FollowUpActive"/> the label is the furthest step reached:
    /// <see cref="CaseDisplayLabel.FollowUp"/> once a referral or appointment exists, otherwise
    /// <see cref="CaseDisplayLabel.Consultation"/> once a consultation was requested, otherwise
    /// <see cref="CaseDisplayLabel.PatientAcknowledged"/> once the notification track is
    /// <see cref="NotificationStatus.Acknowledged"/> or <see cref="NotificationStatus.Understood"/>,
    /// otherwise <see cref="CaseDisplayLabel.PatientNotified"/>.
    /// </para>
    /// <para>This method never mutates the case.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="case"/> or <paramref name="pathway"/> is <see langword="null"/>.</exception>
    public static CaseDisplayLabel From(Case @case, PathwayDefinition pathway)
    {
        ArgumentNullException.ThrowIfNull(@case);
        ArgumentNullException.ThrowIfNull(pathway);

        return From(@case.Stage, @case.NextAction is not null, @case.Notification?.Status,
            @case.Consultation is not null, @case.Referral is not null, @case.Appointment is not null,
            RequiredTracksComplete(@case, pathway));
    }

    /// <summary>Evaluates the same display rules for an immutable historical projection.</summary>
    /// <param name="stage">The recorded stage.</param>
    /// <param name="hasNextAction">Whether a next action was recorded.</param>
    /// <param name="notificationStatus">The recorded notification status, if started.</param>
    /// <param name="hasConsultation">Whether consultation had started.</param>
    /// <param name="hasReferral">Whether referral had started.</param>
    /// <param name="hasAppointment">Whether appointment follow-up had started.</param>
    /// <param name="requiredTracksComplete">Whether every pathway-required track was complete in the projection.</param>
    /// <returns>The display label actually reached at that instant.</returns>
    /// <remarks>Historical metrics must not invent intermediate progress or disagree with current case reads.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stage"/> is unknown.</exception>
    public static CaseDisplayLabel From(CaseStage stage, bool hasNextAction, NotificationStatus? notificationStatus,
        bool hasConsultation, bool hasReferral, bool hasAppointment, bool requiredTracksComplete) =>
        stage switch
        {
            CaseStage.Received => hasNextAction ? CaseDisplayLabel.CaseOpened : CaseDisplayLabel.ResultReceived,
            CaseStage.ReviewPending => CaseDisplayLabel.OwnerAssigned,
            CaseStage.FollowUpActive => hasReferral || hasAppointment ? CaseDisplayLabel.FollowUp
                : hasConsultation ? CaseDisplayLabel.Consultation
                : notificationStatus is NotificationStatus.Acknowledged or NotificationStatus.Understood
                    ? CaseDisplayLabel.PatientAcknowledged : CaseDisplayLabel.PatientNotified,
            CaseStage.ClosureReview => requiredTracksComplete ? CaseDisplayLabel.ClosureReview : CaseDisplayLabel.OutcomeRecorded,
            CaseStage.Closed => CaseDisplayLabel.Closed,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown case stage."),
        };

    private static bool RequiredTracksComplete(Case @case, PathwayDefinition pathway) =>
        pathway.Tracks.Where(t => t.RequiredForClosure).All(t => t.Kind switch
        {
            TrackKind.Notification => @case.Notification?.IsComplete() == true,
            TrackKind.Consultation => @case.Consultation?.IsComplete() == true,
            TrackKind.Referral => @case.Referral?.IsComplete() == true,
            TrackKind.Appointment => @case.Appointment?.IsComplete() == true,
            _ => false,
        });
}
