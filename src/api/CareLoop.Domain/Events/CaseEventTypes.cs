namespace CareLoop.Domain.Events;

/// <summary>
/// Stage-entry event types from contracts/README.md. Supporting events (rejection, escalation,
/// evidence attachment, ...) are added here as their contracts are agreed.
/// </summary>
public static class CaseEventTypes
{
    public const string ResultReceived = "ResultReceived";
    public const string CaseCreated = "CaseCreated";
    public const string OwnerAssigned = "OwnerAssigned";
    public const string PatientNotified = "PatientNotified";
    public const string PatientAcknowledged = "PatientAcknowledged";
    public const string ConsultationRequested = "ConsultationRequested";
    public const string FollowUpStarted = "FollowUpStarted";
    public const string OutcomeDocumented = "OutcomeDocumented";
    public const string ClosureReviewRequested = "ClosureReviewRequested";
    public const string ClosureAuthorised = "ClosureAuthorised";

    /// <summary>Supporting event raised by the deadline worker; it never changes the stage.</summary>
    public const string EscalationRaised = "EscalationRaised";
}
