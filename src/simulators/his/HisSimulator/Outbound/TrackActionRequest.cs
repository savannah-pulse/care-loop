namespace HisSimulator.Outbound;

/// <summary>Source-authored manual consultation, referral, appointment or consent update.</summary>
/// <param name="CaseRef">The local source case.</param>
/// <param name="Performer">The staff actor; optional for a system-reported appointment.</param>
/// <param name="Status">The explicit source status.</param>
/// <param name="ProposedOwner">The proposed practitioner for consultation handoff.</param>
/// <param name="Reason">The documented rejection reason.</param>
/// <param name="ReceivingFacility">The referral destination.</param>
/// <param name="Location">The visibly synthetic appointment location.</param>
/// <param name="ScheduledFor">The appointment's source-recorded time.</param>
/// <param name="CaregiverContact">The consented caregiver's synthetic email address.</param>
public sealed record TrackActionRequest(
    string CaseRef, ActingStaff? Performer, string? Status = null, ActingStaff? ProposedOwner = null,
    string? Reason = null, string? ReceivingFacility = null, string? Location = null,
    DateTimeOffset? ScheduledFor = null, string? CaregiverContact = null);
