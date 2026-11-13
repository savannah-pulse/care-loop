import type { components } from '../api/schema'

type Schemas = components['schemas']

/** One row of the case list, as returned by `GET /cases`. */
export type CaseSummary = Schemas['CaseSummaryResponse']

/** The full coordination state of one case, as returned by `GET /cases/{caseId}`. */
export type CaseDetail = Schemas['CaseDetailResponse']

/** One audit entry of a case timeline, as returned by `GET /cases/{caseId}/timeline`. */
export type TimelineEntry = Schemas['TimelineEntryResponse']

/** Loop-closure metrics, as returned by `GET /metrics`. */
export type Metrics = Schemas['MetricsResponse']

/** The with-and-without-escalation comparison, as returned by `POST /metrics/simulation`. */
export type SimulationResult = Schemas['SimulationResponse']

/** One arm of the escalation comparison: the cohort replayed with or without escalation. */
export type SimulationArm = Schemas['SimulationArmResponse']

/** A handoff summary and where it stands with its reviewer. */
export type HandoffSummary = Schemas['HandoffSummaryResponse']

/** One section of a handoff summary and the facts it cites. */
export type SummarySection = Schemas['SummarySectionResponse']

/** The person or queue accountable for a case. */
export type Owner = Schemas['OwnerResponse']

/** The current state of one parallel track of a case. */
export type TrackStatus = Schemas['TrackStatusResponse']

/** One of the four parallel tracks: notification, consultation, referral or appointment. */
export type TrackKind = Schemas['TrackKind']

/** One of the five pathway stages a case moves through in the API. */
export type CaseStage = Schemas['CaseStage']

/** The kind of event recorded in a timeline entry. */
export type CaseEventType = Schemas['CaseEventType']

/** Why a case was closed. */
export type ClosureReason = NonNullable<Schemas['ClosureReason']>

/** Body of an HTTP 501 answer from an endpoint that is not built yet. */
export type NotImplemented = Schemas['NotImplementedResponse']

/** An RFC 9457 problem description, as the API returns with most errors. */
export type Problem = Schemas['ProblemDetails']
