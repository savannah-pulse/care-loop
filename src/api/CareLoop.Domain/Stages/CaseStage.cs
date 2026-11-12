namespace CareLoop.Domain.Stages;

/// <summary>The ten sequential stages of a coordination case. Numeric values are the stage numbers.</summary>
public enum CaseStage
{
    ResultReceived = 1,
    CaseCreated = 2,
    OwnerAssigned = 3,
    PatientNotified = 4,
    PatientAcknowledged = 5,
    ConsultationPending = 6,
    FollowUpInProgress = 7,
    CareOutcomeDocumented = 8,
    ClosureReview = 9,
    Closed = 10,
}
