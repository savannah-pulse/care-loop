import type { components } from '../api/schema'

type Schemas = components['schemas']

/** A text an AI model wrote for staff to read, as the AI endpoints return it. */
export type AiText = Schemas['AiTextResponse']

/** The channel a message to the patient would go out on. */
export type MessageChannel = Schemas['NotificationChannel']

/** One row of the case list, as returned by `GET /cases`. */
export type CaseSummary = Schemas['CaseSummaryResponse']

/** The full coordination state of one case, as returned by `GET /cases/{caseId}`. */
export type CaseDetail = Schemas['CaseDetailResponse']

/** What a nurse navigator records after a call with the patient, as sent to `POST /cases/{caseId}/call-outcome`. */
export type CallOutcome = Schemas['CallOutcomeRequest']

/** One audit entry of a case timeline, as returned by `GET /cases/{caseId}/timeline`. */
export type TimelineEntry = Schemas['TimelineEntryResponse']

/** Loop-closure metrics, as returned by `GET /metrics`. */
export type Metrics = Schemas['MetricsResponse']

/** The with-and-without-escalation comparison, as returned by `POST /metrics/simulation`. */
export type SimulationResult = Schemas['SimulationResponse']

/** One arm of the escalation comparison: the cohort replayed with or without escalation. */
export type SimulationArm = Schemas['SimulationArmResponse']

/** The person or queue accountable for a case. */
export type Owner = Schemas['OwnerResponse']

/** The current state of one parallel track of a case. */
export type TrackStatus = Schemas['TrackStatusResponse']

/** One of the four parallel tracks: notification, consultation, referral or appointment. */
export type TrackKind = Schemas['TrackKind']

/** One of the five pathway stages a case moves through in the API. */
export type CaseStage = Schemas['CaseStage']

/** One of the ten display stages a case shows, computed by the API from its stage and tracks. */
export type CaseDisplayLabel = Schemas['CaseDisplayLabel']

/** One escalation raised on a case, with who acknowledged it and when the late step was done. */
export type Escalation = Schemas['EscalationResponse']

/** One row of the exception queue, as returned by `GET /exceptions`. */
export type ExceptionItem = Schemas['ExceptionItemResponse']

/** A screening programme's settings, as returned by `GET /programmes` and `GET /programmes/{pathwayId}`. */
export type Programme = Schemas['ProgrammeResponse']

/** How long one next action may take, who holds the case meanwhile and where the value comes from. */
export type DeadlineDefinition = Schemas['DeadlineDefinition']

/** One step of a programme's escalation ladder. */
export type EscalationStep = Schemas['EscalationStep']

/** What a programme requires before a case may close for one reason. */
export type ClosureRule = Schemas['ClosureEvidenceRule']

/** One kind of outcome evidence a programme recognises. */
export type EvidenceKind = Schemas['EvidenceKindDefinition']

/** One entry in a programme's change history. */
export type ProgrammeChange = Schemas['PathwayChangeLogEntry']

/** A rate with the cases it counts and the cases it is out of. */
export type RateMeasure = Schemas['RateMeasureResponse']

/** A median elapsed time, with how many cases reached the step. */
export type DurationMeasure = Schemas['DurationMeasureResponse']

/** The kind of event recorded in a timeline entry. */
export type CaseEventType = Schemas['CaseEventType']

/** Why a case was closed. */
export type ClosureReason = NonNullable<Schemas['ClosureReason']>

/** Body of an HTTP 501 answer from an endpoint that is not built yet. */
export type NotImplemented = Schemas['NotImplementedResponse']

/** An RFC 9457 problem description, as the API returns with most errors. */
export type Problem = Schemas['ProblemDetails']
