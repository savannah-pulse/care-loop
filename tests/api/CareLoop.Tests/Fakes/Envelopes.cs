using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;

namespace CareLoop.Tests.Fakes;

/// <summary>
/// Builders for synthetic <see cref="EventEnvelope"/>s, one per inbound event type. Every identifier is prefixed <c>SYN-</c>.
/// </summary>
public static class Envelopes
{
    /// <summary>The source result every builder refers to unless told otherwise.</summary>
    public const string ResultId = "SYN-RESULT-0001";

    private static int _counter;

    /// <summary>A synthetic clinician actor.</summary>
    public static Actor Clinician(string id = "SYN-STAFF-0001") =>
        new() { Reference = id, Role = "clinician", FacilityId = "SYN-FAC-A" };

    /// <summary>A fresh source event identifier, unique within the test run.</summary>
    public static string NextEventId() => $"SYN-EVT-{Interlocked.Increment(ref _counter):0000}";

    /// <summary>A screening result that opens a case.</summary>
    public static EventEnvelope ScreeningResult(DateTimeOffset at, string resultId = ResultId, Actor? namedOwner = null, string pathwayId = "breast-screening", string? eventId = null, string? patientDisplayName = null, string? patientSecondaryIdentifier = null) =>
        new()
        {
            SourceSystem = TestData.SourceSystem,
            SourceEventId = eventId ?? NextEventId(),
            Type = CaseEventType.ScreeningResultReceived,
            SourceResultId = resultId,
            OccurredAt = at,
            Actor = null,
            Payload = new ScreeningResultPayload
            {
                PatientReference = "SYN-PT-0001",
                PatientDisplayName = patientDisplayName,
                PatientSecondaryIdentifier = patientSecondaryIdentifier,
                PathwayId = pathwayId,
                FacilityId = "SYN-FAC-A",
                ResultReference = $"DiagnosticReport/{resultId}",
                NamedOwner = namedOwner,
                PatientLanguage = "en",
                PatientContactPoints = [new ContactPoint { Channel = NotificationChannel.Simulator, Address = "SYN-INBOX-0001" }],
            },
        };

    /// <summary>A completed clinical review.</summary>
    public static EventEnvelope ClinicalReview(DateTimeOffset at, string urgency = "PLACEHOLDER-URGENCY-A", Actor? reviewer = null, string resultId = ResultId) =>
        new()
        {
            SourceSystem = TestData.SourceSystem,
            SourceEventId = NextEventId(),
            Type = CaseEventType.ClinicalReviewCompleted,
            SourceResultId = resultId,
            OccurredAt = at,
            Actor = reviewer ?? Clinician(),
            Payload = new ClinicalReviewPayload { UrgencyCode = urgency, ReviewTaskReference = "Task/SYN-REVIEW-0001" },
        };

    /// <summary>
    /// A delivery report or patient response on the notification track.
    /// </summary>
    /// <remarks>
    /// The envelope's source system is the system whose result identifier it carries, so the ingestor can
    /// find the case. An inbound notification handler must therefore stamp the result's source system,
    /// not the channel's, when it translates a report.
    /// </remarks>
    public static EventEnvelope Notification(DateTimeOffset at, NotificationUpdateKind kind, NotificationChannel channel = NotificationChannel.Simulator, Guid? attemptId = null, string resultId = ResultId, int? confidence = null) =>
        new()
        {
            SourceSystem = TestData.SourceSystem,
            SourceEventId = NextEventId(),
            Type = CaseEventType.NotificationUpdated,
            SourceResultId = resultId,
            OccurredAt = at,
            Actor = null,
            Payload = new NotificationUpdatePayload
            {
                Kind = kind,
                Channel = channel,
                CommunicationReference = $"Communication/SYN-COMM-{kind}-{channel}",
                AttemptId = attemptId,
                FailureReason = kind == NotificationUpdateKind.Failed ? "PLACEHOLDER: channel unreachable" : null,
                Confidence = confidence,
            },
        };

    /// <summary>The synthetic caregiver contact point used by <see cref="Consent"/>.</summary>
    public static ContactPoint CaregiverContact() =>
        new() { Channel = NotificationChannel.Email, Address = "SYN-CAREGIVER-INBOX-0001" };

    /// <summary>A caregiver consent granted or withdrawn by the patient.</summary>
    public static EventEnvelope Consent(DateTimeOffset at, ConsentUpdateKind kind, ContactPoint? caregiver = null, bool noContact = false, string consentReference = "Consent/SYN-CONSENT-0001", string resultId = ResultId) =>
        new()
        {
            SourceSystem = TestData.SourceSystem,
            SourceEventId = NextEventId(),
            Type = CaseEventType.ConsentRecorded,
            SourceResultId = resultId,
            OccurredAt = at,
            Actor = null,
            Payload = new ConsentPayload
            {
                Kind = kind,
                ConsentReference = consentReference,
                CaregiverContactPoint = noContact ? null : caregiver ?? CaregiverContact(),
            },
        };

    /// <summary>A consultation status change.</summary>
    public static EventEnvelope Consultation(DateTimeOffset at, ConsultationStatus status, CaseOwner? proposedOwner = null, Actor? actor = null, string? reason = null, string resultId = ResultId) =>
        new()
        {
            SourceSystem = TestData.SourceSystem,
            SourceEventId = NextEventId(),
            Type = CaseEventType.ConsultationUpdated,
            SourceResultId = resultId,
            OccurredAt = at,
            Actor = actor,
            Payload = new ConsultationUpdatePayload
            {
                Status = status,
                ServiceRequestReference = "ServiceRequest/SYN-CONSULT-0001",
                TaskReference = $"Task/SYN-CONSULT-0001/{status}",
                ProposedOwner = proposedOwner,
                Reason = reason,
            },
        };

    /// <summary>A referral status change.</summary>
    public static EventEnvelope Referral(DateTimeOffset at, ReferralStatus status, string? reason = null, string resultId = ResultId) =>
        new()
        {
            SourceSystem = TestData.SourceSystem,
            SourceEventId = NextEventId(),
            Type = CaseEventType.ReferralUpdated,
            SourceResultId = resultId,
            OccurredAt = at,
            Actor = Clinician("SYN-STAFF-0002"),
            Payload = new ReferralUpdatePayload
            {
                Status = status,
                ServiceRequestReference = "ServiceRequest/SYN-REF-0001",
                TaskReference = $"Task/SYN-REF-0001/{status}",
                ReceivingFacilityId = "SYN-FAC-B",
                Reason = reason,
            },
        };

    /// <summary>An appointment status change.</summary>
    public static EventEnvelope Appointment(DateTimeOffset at, AppointmentStatus status, DateTimeOffset? scheduledFor = null, string resultId = ResultId) =>
        new()
        {
            SourceSystem = TestData.SourceSystem,
            SourceEventId = NextEventId(),
            Type = CaseEventType.AppointmentUpdated,
            SourceResultId = resultId,
            OccurredAt = at,
            Actor = null,
            Payload = new AppointmentUpdatePayload
            {
                Status = status,
                AppointmentReference = "Appointment/SYN-APPT-0001",
                ScheduledFor = scheduledFor,
            },
        };

    /// <summary>Recorded outcome evidence.</summary>
    public static EventEnvelope Outcome(DateTimeOffset at, string evidenceReference = "Procedure/SYN-PROC-0001", string resultId = ResultId, string evidenceKind = "diagnosis", Actor? actor = null) =>
        new()
        {
            SourceSystem = TestData.SourceSystem,
            SourceEventId = NextEventId(),
            Type = CaseEventType.OutcomeRecorded,
            SourceResultId = resultId,
            OccurredAt = at,
            Actor = actor ?? Clinician(),
            Payload = new OutcomePayload { EvidenceReference = evidenceReference, EvidenceKind = evidenceKind },
        };

    /// <summary>A closure decision.</summary>
    public static EventEnvelope Closure(DateTimeOffset at, ClosureReason reason = ClosureReason.Treated, string? evidenceReference = "Procedure/SYN-PROC-0001", Actor? authorizedBy = null, bool noAuthorizer = false, string? exceptionNote = null, string resultId = ResultId) =>
        new()
        {
            SourceSystem = TestData.SourceSystem,
            SourceEventId = NextEventId(),
            Type = CaseEventType.ClosureDecided,
            SourceResultId = resultId,
            OccurredAt = at,
            Actor = noAuthorizer ? null : authorizedBy ?? Clinician(),
            Payload = new ClosurePayload
            {
                Reason = reason,
                ProvenanceReference = "Provenance/SYN-PROV-0001",
                EvidenceReference = evidenceReference,
                ExceptionNote = exceptionNote,
            },
        };
}
