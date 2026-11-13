namespace CareLoop.Domain.Tracks;

/// <summary>
/// Identifies one of the parallel follow-up tracks of a case.
/// </summary>
/// <remarks>
/// <para>
/// <b>Definition of a track.</b> A track is one strand of follow-up work on a
/// <see cref="CareLoop.Domain.Cases.Case"/>. The four tracks run in parallel while the case is in
/// <see cref="CareLoop.Domain.Cases.CaseStage.FollowUpActive"/>, and a case has at most one of each.
/// Each track has its own statuses and its own completion rule, which is deliberately stricter
/// than "something was sent".
/// </para>
/// <list type="table">
/// <listheader><term>Track</term><description>Complete when</description></listheader>
/// <item><term><see cref="NotificationTrack"/></term><description>The patient acknowledges; delivery alone does not count.</description></item>
/// <item><term><see cref="ConsultationTrack"/></term><description>The consultation outcome is recorded.</description></item>
/// <item><term><see cref="ReferralTrack"/></term><description>The referral is accepted, which still is not completed care.</description></item>
/// <item><term><see cref="AppointmentTrack"/></term><description>The patient attended and an outcome is documented.</description></item>
/// </list>
/// <para>
/// A track that fails (unreachable, rejected, missed) never parks the case: it creates a next
/// action with an owner and a deadline. The pathway definition says which tracks are required for
/// closure and which next action each failure creates.
/// </para>
/// </remarks>
public enum TrackKind
{
    /// <summary>Telling the patient and getting their acknowledgement.</summary>
    Notification = 0,

    /// <summary>Consultation and the ownership handoff it carries.</summary>
    Consultation = 1,

    /// <summary>Referral to another service.</summary>
    Referral = 2,

    /// <summary>The follow-up appointment.</summary>
    Appointment = 3,
}
