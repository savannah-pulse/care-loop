namespace CareLoop.Domain.Stages;

/// <summary>Descriptor for one stage; <see cref="Description"/> follows docs/architecture.md.</summary>
public sealed record StageDefinition(CaseStage Stage, string Code, string EntryEventType, string Description)
{
    public int Number => (int)Stage;
}

/// <summary>Single source of truth for the ten-stage journey: codes, order, and entry events.</summary>
public static class StageCatalog
{
    public static IReadOnlyList<StageDefinition> All { get; } =
    [
        new(CaseStage.ResultReceived, "RESULT_RECEIVED", Events.CaseEventTypes.ResultReceived,
            "Synthetic screening result arrives through the HIS adapter."),
        new(CaseStage.CaseCreated, "CASE_CREATED", Events.CaseEventTypes.CaseCreated,
            "Result is validated and deduplicated; one coordination case is opened."),
        new(CaseStage.OwnerAssigned, "OWNER_ASSIGNED", Events.CaseEventTypes.OwnerAssigned,
            "A coordinator is assigned or the case enters an accountable facility lead queue."),
        new(CaseStage.PatientNotified, "PATIENT_NOTIFIED", Events.CaseEventTypes.PatientNotified,
            "Channel simulator reports delivery; delivery is not acknowledgement."),
        new(CaseStage.PatientAcknowledged, "PATIENT_ACKNOWLEDGED", Events.CaseEventTypes.PatientAcknowledged,
            "Patient acknowledgement and any separately recorded understanding are captured."),
        new(CaseStage.ConsultationPending, "CONSULTATION_PENDING", Events.CaseEventTypes.ConsultationRequested,
            "A consultation or handoff request is recorded with its proposed owner."),
        new(CaseStage.FollowUpInProgress, "FOLLOW_UP_IN_PROGRESS", Events.CaseEventTypes.FollowUpStarted,
            "Referral and appointment events are correlated; acceptance is not completed care."),
        new(CaseStage.CareOutcomeDocumented, "CARE_OUTCOME_DOCUMENTED", Events.CaseEventTypes.OutcomeDocumented,
            "An authorized source supplies an outcome event and clinical record reference."),
        new(CaseStage.ClosureReview, "CLOSURE_REVIEW", Events.CaseEventTypes.ClosureReviewRequested,
            "Evidence or an authorized exception is awaiting a human closure decision."),
        new(CaseStage.Closed, "CLOSED", Events.CaseEventTypes.ClosureAuthorised,
            "An authorized closure event with typed reason and evidence is accepted and audited."),
    ];

    public static StageDefinition Get(CaseStage stage) => All[(int)stage - 1];

    public static CaseStage First => CaseStage.ResultReceived;

    public static CaseStage Last => CaseStage.Closed;
}
