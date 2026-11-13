using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;

namespace CareLoop.Domain.Cases;

/// <summary>
/// One actionable screening result, followed until it is closed with evidence. The central record of CareLoop.
/// </summary>
/// <remarks>
/// <para>
/// <b>Definition.</b> One actionable result equals one case. A case is a coordination record, not a
/// clinical record: it answers "what stage is this result at, who is accountable, what must happen
/// next and by when, and how did it end?". The hospital systems remain the systems of record, so a
/// case holds identifiers and references into them and never clinical content.
/// </para>
/// <para><b>What a case holds.</b></para>
/// <list type="bullet">
/// <item><description><b>Source identity</b>: <see cref="SourceSystem"/>, <see cref="SourceResultId"/>, <see cref="PatientReference"/> and <see cref="FacilityId"/>. One source result can open only one case.</description></item>
/// <item><description><b>Pathway</b>: <see cref="PathwayId"/> names the <see cref="CareLoop.Domain.Pathways.PathwayDefinition"/> that supplies this case's stage rules, deadlines, escalation ladder and closure rules.</description></item>
/// <item><description><b>Urgency</b>: <see cref="Urgency"/> is the clinician-reviewed urgency code received from the hospital system. CareLoop never sets or changes it.</description></item>
/// <item><description><b>Stage</b>: <see cref="Stage"/> is where the case is in the pathway (see <see cref="CaseStage"/>).</description></item>
/// <item><description><b>Owner</b>: <see cref="Owner"/> is the one person or queue accountable right now. <see cref="PendingAssignment"/> is a proposed handoff that has not been accepted; until it is, the current owner stays accountable.</description></item>
/// <item><description><b>Next action and deadline</b>: <see cref="NextAction"/> is the single thing that must happen next and <see cref="DueAt"/> is when it is due. The deadline always comes from the pathway definition.</description></item>
/// <item><description><b>Tracks</b>: <see cref="Notification"/>, <see cref="Consultation"/>, <see cref="Referral"/> and <see cref="Appointment"/> are the strands of follow-up work that run in parallel (see <see cref="TrackKind"/>). Each is <see langword="null"/> until it starts.</description></item>
/// <item><description><b>Exceptions and escalation</b>: <see cref="Exceptions"/> flags what is currently wrong (overdue, rejected, unreachable and so on) and <see cref="IsEscalated"/> says oversight has been brought in. Both are flags, not stages, so the case keeps its place in the pathway.</description></item>
/// <item><description><b>Ending</b>: <see cref="OutcomeEvidenceReference"/> points at the outcome evidence in the hospital system and <see cref="Closure"/> is the authorized human decision that closed the case.</description></item>
/// <item><description><b>Caregiver consent</b>: <see cref="CaregiverConsent"/> records, with evidence, that the patient agreed for one caregiver to receive appointment reminders. It is the only contact data a case holds, and it goes when the consent is withdrawn.</description></item>
/// </list>
/// <para>
/// <b>How a case changes.</b> Only through the methods on this class, which the rules in
/// <c>CareLoop.Domain.Rules</c> guard, and it is persisted only through
/// <see cref="ICaseRepository.SaveAsync"/>. When a change means the outside world must be told, the
/// case raises a domain event (see <see cref="DomainEvents"/>); it never sends anything itself.
/// </para>
/// <para>
/// <b>Why it exists.</b> Follow-up fails in the gaps between systems and people. Holding stage,
/// owner, deadline and closure evidence in one record is what makes it impossible for a result to
/// be silently dropped.
/// </para>
/// </remarks>
public sealed class Case
{
    private readonly List<Escalation> _escalations = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Opens a case for a newly received actionable result.
    /// </summary>
    /// <param name="id">CareLoop's identifier for the new case.</param>
    /// <param name="sourceSystem">Identifier of the system that produced the result.</param>
    /// <param name="sourceResultId">The source system's identifier of the actionable result; one result equals one case.</param>
    /// <param name="patientReference">Source-system reference of the patient. Never a name.</param>
    /// <param name="pathwayId">Identifier of the pathway definition the case follows.</param>
    /// <param name="facilityId">The facility that produced the result.</param>
    /// <param name="initialOwner">The first accountable owner, or the facility lead queue when nobody is named. Required because a case may never be unowned.</param>
    /// <param name="receivedAt">When CareLoop received the result.</param>
    /// <exception cref="ArgumentException">A required identifier is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="initialOwner"/> is <see langword="null"/>.</exception>
    public Case(
        Guid id,
        string sourceSystem,
        string sourceResultId,
        string patientReference,
        string pathwayId,
        string facilityId,
        CaseOwner initialOwner,
        DateTimeOffset receivedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceResultId);
        ArgumentException.ThrowIfNullOrWhiteSpace(patientReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathwayId);
        ArgumentException.ThrowIfNullOrWhiteSpace(facilityId);
        ArgumentNullException.ThrowIfNull(initialOwner);

        Id = id;
        SourceSystem = sourceSystem;
        SourceResultId = sourceResultId;
        PatientReference = patientReference;
        PathwayId = pathwayId;
        FacilityId = facilityId;
        Owner = initialOwner;
        Exceptions = initialOwner.IsQueue ? CaseExceptions.Unassigned : CaseExceptions.None;
        Stage = CaseStage.Received;
        CreatedAt = receivedAt;
        UpdatedAt = receivedAt;
    }

    /// <summary>
    /// Used only by the persistence layer when materializing a stored case.
    /// </summary>
    private Case()
    {
        SourceSystem = null!;
        SourceResultId = null!;
        PatientReference = null!;
        PathwayId = null!;
        FacilityId = null!;
        Owner = null!;
    }

    /// <summary>CareLoop's identifier of the case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identifier of the system that produced the result.</summary>
    public string SourceSystem { get; private set; }

    /// <summary>The source system's identifier of the actionable result. Unique together with <see cref="SourceSystem"/>.</summary>
    public string SourceResultId { get; private set; }

    /// <summary>Source-system reference of the patient. Never a name or other clinical or demographic detail.</summary>
    public string PatientReference { get; private set; }

    /// <summary>Identifier of the pathway definition that supplies this case's stages, deadlines and evidence rules.</summary>
    public string PathwayId { get; private set; }

    /// <summary>The facility that produced the result; decides which facility lead queue applies.</summary>
    public string FacilityId { get; private set; }

    /// <summary>The clinician-reviewed urgency code, or <see langword="null"/> until the review arrives. CareLoop never sets or changes it on its own.</summary>
    public string? Urgency { get; private set; }

    /// <summary>The pathway stage the case is in.</summary>
    public CaseStage Stage { get; private set; }

    /// <summary>The one owner accountable for the next action. Never <see langword="null"/>.</summary>
    public CaseOwner Owner { get; private set; }

    /// <summary>The ownership transfer awaiting a response, if any. While it is pending, <see cref="Owner"/> stays accountable.</summary>
    public Assignment? PendingAssignment { get; private set; }

    /// <summary>Key of the single next action the owner must take, as named in the pathway definition.</summary>
    public string? NextAction { get; private set; }

    /// <summary>When the next action is due. Always computed from the pathway definition, never hard-coded.</summary>
    public DateTimeOffset? DueAt { get; private set; }

    /// <summary><see langword="true"/> while the case has an open escalation. A flag, not a stage.</summary>
    public bool IsEscalated { get; private set; }

    /// <summary>The unresolved exceptions that put the case in the exception queue.</summary>
    public CaseExceptions Exceptions { get; private set; }

    /// <summary>Every escalation raised on the case, oldest first.</summary>
    public IReadOnlyList<Escalation> Escalations => _escalations;

    /// <summary>The notification track, or <see langword="null"/> until a notification is requested.</summary>
    public NotificationTrack? Notification { get; private set; }

    /// <summary>The consultation track, or <see langword="null"/> until a consultation is requested.</summary>
    public ConsultationTrack? Consultation { get; private set; }

    /// <summary>The referral track, or <see langword="null"/> until a referral is sent.</summary>
    public ReferralTrack? Referral { get; private set; }

    /// <summary>The appointment track, or <see langword="null"/> until an appointment is scheduled.</summary>
    public AppointmentTrack? Appointment { get; private set; }

    /// <summary>Reference to the recorded outcome evidence, or <see langword="null"/> until an outcome event arrives.</summary>
    public string? OutcomeEvidenceReference { get; private set; }

    /// <summary>The accepted closure decision, or <see langword="null"/> while the case is open.</summary>
    public ClosureDecision? Closure { get; private set; }

    /// <summary>The patient's consent for a caregiver to be told about appointments, or <see langword="null"/> when none was ever recorded. Check <see cref="Consent.IsActive"/> before contacting the caregiver.</summary>
    public Consent? CaregiverConsent { get; private set; }

    /// <summary>When the case was opened.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the case last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token, incremented by the repository on every save.</summary>
    public int Version { get; private set; }

    /// <summary>Domain events raised since the case was loaded; the repository writes them to the outbox and clears them.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary><see langword="true"/> once the case has been closed. A closed case is final and refuses every further change.</summary>
    public bool IsClosed => Stage == CaseStage.Closed;

    /// <summary>
    /// Queues a domain event to be written to the outbox with the next save.
    /// </summary>
    /// <param name="domainEvent">The event to publish once the change is committed.</param>
    public void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Removes the queued domain events after the repository has stored them in the outbox.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Records the clinician-reviewed urgency.
    /// </summary>
    /// <param name="urgencyCode">The urgency code from the review; must be one the pathway definition knows.</param>
    /// <param name="reviewer">The clinician who reviewed the result.</param>
    /// <param name="at">When the review was completed.</param>
    /// <remarks>Urgency always arrives clinician-reviewed from the HIS. This method stores it exactly as received; it must never derive or adjust it. Checking the code against the pathway's known codes is the caller's job, because the Domain has no pathway here.</remarks>
    /// <exception cref="ArgumentException"><paramref name="urgencyCode"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="reviewer"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The case is closed.</exception>
    public void RecordReview(string urgencyCode, Actor reviewer, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urgencyCode);
        ArgumentNullException.ThrowIfNull(reviewer);
        ThrowIfClosed();

        Urgency = urgencyCode;
        Touch(at);
    }

    /// <summary>
    /// Moves the case to another stage after <see cref="Rules.TransitionRules"/> allowed the move.
    /// </summary>
    /// <param name="stage">The stage to enter.</param>
    /// <param name="at">When the move happened.</param>
    /// <remarks>Entering <see cref="CaseStage.Closed"/> is possible only through <see cref="Close"/>, which needs the authorized decision, so no caller can close a case by moving it.</remarks>
    /// <exception cref="InvalidOperationException">The case is closed, which is final, or <paramref name="stage"/> is <see cref="CaseStage.Closed"/>.</exception>
    public void MoveTo(CaseStage stage, DateTimeOffset at)
    {
        ThrowIfClosed();
        if (stage == CaseStage.Closed)
        {
            throw new InvalidOperationException(
                "A case cannot be moved to Closed; closure requires an authorized decision through Close.");
        }

        Stage = stage;
        Touch(at);
    }

    /// <summary>
    /// Sets the single next action and its deadline.
    /// </summary>
    /// <param name="nextAction">Key of the next action, as named in the pathway definition.</param>
    /// <param name="dueAt">The deadline computed by <see cref="Rules.DeadlinePolicy"/>.</param>
    /// <param name="at">When the next action was set.</param>
    /// <remarks>Every exception must end here: a case is never parked without a next action, an owner and a deadline. Setting a new next action starts a new clock, so the overdue flag of the previous one is cleared.</remarks>
    /// <exception cref="ArgumentException"><paramref name="nextAction"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The case is closed.</exception>
    public void SetNextAction(string nextAction, DateTimeOffset dueAt, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nextAction);
        ThrowIfClosed();

        NextAction = nextAction;
        DueAt = dueAt;
        Exceptions &= ~CaseExceptions.Overdue;
        Touch(at);
    }

    /// <summary>
    /// Records a proposed ownership transfer without changing the accountable owner.
    /// </summary>
    /// <param name="proposal">The proposal produced by <see cref="Rules.OwnershipRules"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="proposal"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Another proposal is already pending, or the case is closed.</exception>
    public void ProposeOwner(Assignment proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ThrowIfClosed();
        if (PendingAssignment is not null)
        {
            throw new InvalidOperationException(
                $"A handoff to '{PendingAssignment.ProposedOwner.Reference}' is already pending; it must be accepted or rejected first.");
        }

        PendingAssignment = proposal with { Status = AssignmentStatus.Proposed, RespondedAt = null, RejectionReason = null };
        Touch(proposal.ProposedAt);

        Raise(new HandoffProposed
        {
            EventId = Guid.NewGuid(),
            CaseId = Id,
            OccurredAt = proposal.ProposedAt,
            CurrentOwner = Owner,
            ProposedOwner = proposal.ProposedOwner,
            RespondBy = proposal.RespondBy,
        });
    }

    /// <summary>
    /// Completes a pending transfer: the proposed owner becomes the accountable owner.
    /// </summary>
    /// <param name="acceptedBy">The person accepting; must be the proposed owner.</param>
    /// <param name="at">When the transfer was accepted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="acceptedBy"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No proposal is pending, it was proposed to someone else, or the case is closed.</exception>
    public void AcceptOwnership(Actor acceptedBy, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(acceptedBy);
        ThrowIfClosed();
        var proposal = RequirePendingProposalFor(acceptedBy);

        Owner = proposal.ProposedOwner;
        PendingAssignment = null;
        Exceptions &= ~CaseExceptions.Unassigned;
        Touch(at);
    }

    /// <summary>
    /// Refuses a pending transfer: the current owner keeps the case and the next action.
    /// </summary>
    /// <param name="rejectedBy">The person refusing; must be the proposed owner.</param>
    /// <param name="reason">The reason given.</param>
    /// <param name="at">When the transfer was refused.</param>
    /// <remarks>The case is flagged <see cref="CaseExceptions.Rejected"/> so it appears in the exception queue until the owner has arranged the alternative and set a new next action.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="rejectedBy"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">No proposal is pending, it was proposed to someone else, or the case is closed.</exception>
    public void RejectOwnership(Actor rejectedBy, string reason, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(rejectedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ThrowIfClosed();
        var proposal = RequirePendingProposalFor(rejectedBy);

        PendingAssignment = null;
        Exceptions |= CaseExceptions.Rejected;
        Touch(at);

        Raise(new HandoffRejected
        {
            EventId = Guid.NewGuid(),
            CaseId = Id,
            OccurredAt = at,
            CurrentOwner = Owner,
            RejectedBy = proposal.ProposedOwner,
            Reason = reason,
        });
    }

    /// <summary>
    /// Assigns an owner directly, used only for the first owner and for the facility lead queue.
    /// </summary>
    /// <param name="owner">The new accountable owner.</param>
    /// <param name="at">When the assignment was made.</param>
    /// <remarks>Transfers between people must use propose and accept instead, so responsibility is never dropped. Assigning a queue flags the case <see cref="CaseExceptions.Unassigned"/>; assigning a person clears that flag.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The case is closed.</exception>
    public void AssignOwner(CaseOwner owner, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ThrowIfClosed();

        Owner = owner;
        if (owner.IsQueue)
        {
            Exceptions |= CaseExceptions.Unassigned;
        }
        else
        {
            Exceptions &= ~CaseExceptions.Unassigned;
        }

        Touch(at);
    }

    /// <summary>
    /// Starts the notification track and requests a patient notification.
    /// </summary>
    /// <param name="templateKey">Key of the approved template to send.</param>
    /// <param name="at">When the notification was requested.</param>
    /// <returns>The started track.</returns>
    /// <remarks>Requesting a notification does not complete anything, transfer ownership or stop a clock: only the patient's acknowledgement completes the track.</remarks>
    /// <exception cref="ArgumentException"><paramref name="templateKey"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The notification track has already started, or the case is closed.</exception>
    public NotificationTrack BeginNotification(string templateKey, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ThrowIfClosed();
        ThrowIfTrackStarted(Notification, TrackKind.Notification);

        Notification = new NotificationTrack(at);
        Touch(at);

        Raise(new NotificationRequested
        {
            EventId = Guid.NewGuid(),
            CaseId = Id,
            OccurredAt = at,
            PatientReference = PatientReference,
            TemplateKey = templateKey,
        });

        return Notification;
    }

    /// <summary>
    /// Starts the consultation track.
    /// </summary>
    /// <param name="sourceReference">Reference to the consultation <c>ServiceRequest</c>.</param>
    /// <param name="at">When the consultation was requested.</param>
    /// <returns>The started track, in status <see cref="ConsultationStatus.Requested"/>.</returns>
    /// <exception cref="InvalidOperationException">The consultation track has already started, or the case is closed.</exception>
    public ConsultationTrack BeginConsultation(string sourceReference, DateTimeOffset at)
    {
        ThrowIfClosed();
        ThrowIfTrackStarted(Consultation, TrackKind.Consultation);

        Consultation = new ConsultationTrack(sourceReference, at);
        Touch(at);
        return Consultation;
    }

    /// <summary>
    /// Starts the referral track.
    /// </summary>
    /// <param name="sourceReference">Reference to the referral <c>ServiceRequest</c>.</param>
    /// <param name="at">When the referral was sent.</param>
    /// <returns>The started track, in status <see cref="ReferralStatus.Sent"/>.</returns>
    /// <exception cref="InvalidOperationException">The referral track has already started, or the case is closed.</exception>
    public ReferralTrack BeginReferral(string sourceReference, DateTimeOffset at)
    {
        ThrowIfClosed();
        ThrowIfTrackStarted(Referral, TrackKind.Referral);

        Referral = new ReferralTrack(sourceReference, at);
        Touch(at);
        return Referral;
    }

    /// <summary>
    /// Starts the appointment track.
    /// </summary>
    /// <param name="sourceReference">Reference to the <c>Appointment</c>.</param>
    /// <param name="scheduledFor">When the appointment is scheduled for.</param>
    /// <param name="at">When the appointment was booked.</param>
    /// <returns>The started track, in status <see cref="AppointmentStatus.Scheduled"/>.</returns>
    /// <remarks>If outcome evidence was already recorded on the case, it is linked to the new track so the order in which the hospital system reports attendance and outcome does not matter.</remarks>
    /// <exception cref="InvalidOperationException">The appointment track has already started, or the case is closed.</exception>
    public AppointmentTrack BeginAppointment(string sourceReference, DateTimeOffset scheduledFor, DateTimeOffset at)
    {
        ThrowIfClosed();
        ThrowIfTrackStarted(Appointment, TrackKind.Appointment);

        Appointment = new AppointmentTrack(sourceReference, scheduledFor, at);
        if (OutcomeEvidenceReference is not null)
        {
            Appointment.RecordOutcome(OutcomeEvidenceReference, at);
        }

        Touch(at);
        return Appointment;
    }

    /// <summary>
    /// Flags an unresolved exception so the case appears in the exception queue.
    /// </summary>
    /// <param name="exception">The exception flag or flags to set.</param>
    /// <param name="at">When the exception was detected.</param>
    /// <remarks>Flagging never parks the case: the caller must also give it a next action with an owner and a deadline through <see cref="SetNextAction"/>.</remarks>
    /// <exception cref="InvalidOperationException">The case is closed.</exception>
    public void FlagException(CaseExceptions exception, DateTimeOffset at)
    {
        ThrowIfClosed();

        Exceptions |= exception;
        Touch(at);
    }

    /// <summary>
    /// Clears an exception once it has been resolved.
    /// </summary>
    /// <param name="exception">The exception flag or flags to clear.</param>
    /// <param name="at">When the exception was resolved.</param>
    public void ClearException(CaseExceptions exception, DateTimeOffset at)
    {
        Exceptions &= ~exception;
        Touch(at);
    }

    /// <summary>
    /// Raises an escalation on the case.
    /// </summary>
    /// <param name="escalation">The escalation step reached, from the pathway's escalation ladder.</param>
    /// <remarks>Escalation adds oversight only: it changes neither the stage nor the owner, who remains accountable for the overdue action.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="escalation"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The case is closed.</exception>
    public void Escalate(Escalation escalation)
    {
        ArgumentNullException.ThrowIfNull(escalation);
        ThrowIfClosed();

        _escalations.Add(escalation with { ResolvedAt = null });
        IsEscalated = true;
        Touch(escalation.RaisedAt);

        Raise(new EscalationRaised
        {
            EventId = Guid.NewGuid(),
            CaseId = Id,
            OccurredAt = escalation.RaisedAt,
            Level = escalation.Level,
            EscalatedTo = escalation.EscalatedTo,
            CurrentOwner = Owner,
            Reason = escalation.Reason,
        });
    }

    /// <summary>
    /// Marks every open escalation as resolved once the overdue action has been completed.
    /// </summary>
    /// <param name="at">When the overdue action was completed.</param>
    public void ResolveEscalations(DateTimeOffset at)
    {
        for (var i = 0; i < _escalations.Count; i++)
        {
            if (_escalations[i].ResolvedAt is null)
            {
                _escalations[i] = _escalations[i] with { ResolvedAt = at };
            }
        }

        IsEscalated = false;
        Touch(at);
    }

    /// <summary>
    /// Records the reference to the outcome evidence.
    /// </summary>
    /// <param name="evidenceReference">Reference to the <c>Procedure</c> or follow-up <c>DiagnosticReport</c>.</param>
    /// <param name="at">When the outcome was recorded.</param>
    /// <remarks>Recording an outcome does not close the case; it only makes an authorized closure decision possible. The evidence is also linked to the appointment track, which completes only when the patient attended and an outcome is documented.</remarks>
    /// <exception cref="ArgumentException"><paramref name="evidenceReference"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The case is closed.</exception>
    public void RecordOutcomeEvidence(string evidenceReference, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceReference);
        ThrowIfClosed();

        OutcomeEvidenceReference = evidenceReference;
        Appointment?.RecordOutcome(evidenceReference, at);
        Touch(at);
    }

    /// <summary>
    /// Closes the case on a decision that <see cref="Rules.ClosureGate"/> has allowed.
    /// </summary>
    /// <param name="decision">The authorized closure decision with its evidence or documented exception.</param>
    /// <param name="at">When the closure was applied.</param>
    /// <remarks>Closure is final. The next action, deadline, exception flags, pending handoff and open escalations are all cleared, because nothing remains to be done and nobody is accountable for a closed case.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The case is already closed.</exception>
    public void Close(ClosureDecision decision, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ThrowIfClosed();

        Closure = decision;
        Stage = CaseStage.Closed;
        NextAction = null;
        DueAt = null;
        Exceptions = CaseExceptions.None;
        PendingAssignment = null;
        ResolveEscalations(at);
        Touch(at);

        Raise(new CaseClosed
        {
            EventId = Guid.NewGuid(),
            CaseId = Id,
            OccurredAt = at,
            Reason = decision.Reason,
            AuthorizedBy = decision.AuthorizedBy,
        });
    }

    /// <summary>
    /// Records the patient's consent for a caregiver to receive appointment reminders.
    /// </summary>
    /// <param name="caregiverContactPoint">How the caregiver is reached.</param>
    /// <param name="evidenceReference">Reference to the record of the consent in the source system.</param>
    /// <param name="at">When the patient granted the consent according to the source.</param>
    /// <remarks>
    /// Consent is recorded only from an event that carries evidence; CareLoop never infers it. One
    /// active consent at a time: a change of caregiver is a withdrawal followed by a new consent, so
    /// the audit trail shows both decisions. The change is audited by the repository when the event
    /// that carried it is saved. Recording consent changes nothing else about the case.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="caregiverContactPoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="evidenceReference"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">An active consent already exists, or the case is closed.</exception>
    public void RecordConsent(ContactPoint caregiverContactPoint, string evidenceReference, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(caregiverContactPoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceReference);
        ThrowIfClosed();
        if (CaregiverConsent is { IsActive: true })
        {
            throw new InvalidOperationException(
                "A caregiver consent is already active on this case; it must be withdrawn before another is recorded.");
        }

        CaregiverConsent = new Consent
        {
            CaregiverContactPoint = caregiverContactPoint,
            GrantedAt = at,
            EvidenceReference = evidenceReference,
        };
        Touch(at);
    }

    /// <summary>
    /// Records that the patient withdrew the caregiver consent. No further caregiver message may be sent.
    /// </summary>
    /// <param name="evidenceReference">Reference to the record of the withdrawal in the source system, when there is one.</param>
    /// <param name="at">When the consent was withdrawn according to the source.</param>
    /// <remarks>
    /// Withdrawal is allowed even on a closed case, because a consent the patient has taken back
    /// must never look active. The change is audited by the repository when the event that carried
    /// it is saved.
    /// </remarks>
    /// <exception cref="InvalidOperationException">No active consent exists.</exception>
    public void WithdrawConsent(string? evidenceReference, DateTimeOffset at)
    {
        if (CaregiverConsent is not { IsActive: true } consent)
        {
            throw new InvalidOperationException("No caregiver consent is active on this case, so there is nothing to withdraw.");
        }

        CaregiverConsent = consent with { WithdrawnAt = at, WithdrawalEvidenceReference = evidenceReference };
        Touch(at);
    }

    private void Touch(DateTimeOffset at) => UpdatedAt = at > UpdatedAt ? at : UpdatedAt;

    private void ThrowIfClosed()
    {
        if (IsClosed)
        {
            throw new InvalidOperationException($"Case {Id} is closed; a closed case is final and cannot change.");
        }
    }

    private static void ThrowIfTrackStarted(object? track, TrackKind kind)
    {
        if (track is not null)
        {
            throw new InvalidOperationException($"The {kind} track has already started; a case has at most one of each track.");
        }
    }

    private Assignment RequirePendingProposalFor(Actor responder)
    {
        if (PendingAssignment is null)
        {
            throw new InvalidOperationException($"Case {Id} has no pending handoff to respond to.");
        }

        if (!string.Equals(PendingAssignment.ProposedOwner.Reference, responder.Reference, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The handoff was proposed to '{PendingAssignment.ProposedOwner.Reference}', not to '{responder.Reference}'; nobody can respond on another person's behalf.");
        }

        return PendingAssignment;
    }
}
