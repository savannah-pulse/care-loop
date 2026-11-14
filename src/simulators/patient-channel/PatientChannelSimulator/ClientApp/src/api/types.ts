/**
 * Hand-written types mirroring the JSON contracts of the patient-channel simulator's .NET host.
 *
 * @remarks
 * Every record is synthetic and carries a `simulated` (or `synthetic`) marker that the UI must show.
 */

/** The simulated channel a message was presented on. */
export type InboxChannel = 'App' | 'Sms' | 'Portal' | 'Email';

/**
 * Who an inbox message was sent to.
 *
 * @remarks
 * A caregiver's copy is read-only: nothing a caregiver does may count as the patient's response.
 */
export type RecipientKind = 'Patient' | 'Caregiver';

/**
 * What the simulator reported to CareLoop about delivery.
 *
 * @remarks
 * Delivery is a transport fact only; a delivered message is not an acknowledgement.
 */
export type DeliveryState = 'Pending' | 'Delivered' | 'Failed' | 'Silent';

/** The simulated patient's latest response to a message. */
export type ResponseState = 'None' | 'Acknowledged' | 'Understood' | 'CallRequested';

/** One simulated message in the simulated patient inbox (`GET /api/inbox`). */
export interface InboxMessage {
  /** Simulator-local identifier of the message. */
  messageId: string;
  /** Logical id of the FHIR `CommunicationRequest` that produced the message. */
  communicationRequestId: string;
  /** Logical id of the CareLoop case `Task`, when the request referenced one. */
  caseTaskId: string | null;
  /** Synthetic identifier of whoever the message is for (prefixed `SYN-`): the patient, or a caregiver's inbox address. */
  patientSyntheticId: string;
  /** Visibly synthetic display name (starts with "Synthetic"). */
  patientDisplayName: string;
  /** Channel the message was presented on. */
  channel: InboxChannel;
  /** BCP 47 language tag of the message content. */
  language: string;
  /** Message text exactly as CareLoop rendered it. */
  body: string;
  /** ISO 8601 time the simulator received the request. */
  receivedAt: string;
  /** Delivery outcome reported to CareLoop. */
  delivery: DeliveryState;
  /** Reason reported with a failed delivery, otherwise `null`. */
  deliveryFailureReason: string | null;
  /** The patient's latest response. */
  response: ResponseState;
  /** ISO 8601 time of the latest response, otherwise `null`. */
  respondedAt: string | null;
  /** Simulated-data marker; always `true`. */
  simulated: boolean;
  /** Whether the message is for the patient or is a caregiver's copy. */
  recipientKind: RecipientKind;
  /** The patient's latest 1 to 5 confidence rating, otherwise `null`. A rating is not an acknowledgement. */
  confidence: number | null;
  /** ISO 8601 time of the latest rating, otherwise `null`. */
  confidenceRatedAt: string | null;
  /** How many call-me requests CareLoop has accepted for this message. */
  callRequestCount: number;
  /** How many confidence ratings CareLoop has accepted for this message. */
  confidenceRatingCount: number;
}

/**
 * A one-click action a patient can take on an inbox message.
 *
 * @remarks
 * `acknowledge` is "I will come": it sends an acknowledgement and nothing else. The confidence
 * rating and the SMS reply carry a value, so they have their own client functions.
 */
export type InboxAction = 'acknowledge' | 'understand' | 'call-me';

/** What the channel reports when a notification for a patient arrives. */
export type DeliveryBehaviour = 'Deliver' | 'Fail' | 'StaySilent';

/** What a patient does, on their own, after a notification has been delivered to them. */
export type PatientResponseBehaviour =
  'None' | 'Acknowledge' | 'Understand' | 'CallMe' | 'RateConfidence';

/**
 * How one simulated patient reacts when CareLoop notifies them.
 *
 * @remarks
 * A reaction, not a timetable: it is applied only when a notification actually arrives.
 */
export interface PatientBehaviour {
  /** What the channel reports for each notification the patient receives. */
  delivery: DeliveryBehaviour;
  /** What the patient does after a delivered notification; `None` unless `delivery` is `Deliver`. */
  response: PatientResponseBehaviour;
  /** Reason sent with a failed delivery; required when `delivery` is `Fail`. */
  failureReason: string | null;
  /** Delay between delivery and the response, as a .NET duration such as `01:00:00`. */
  respondAfter: string;
  /** Which received notification the patient responds to, counting from 1; 2 means only a reminder. */
  respondToNotification: number;
  /** Simulated-data marker; always `true`. */
  simulated: boolean;
  /**
   * The 1 to 5 confidence rating the patient gives, or `null` for none. Required when `response`
   * is `RateConfidence`; with another response the patient rates straight after it.
   */
  confidence: number | null;
}

/** The behaviour in force for one patient (`GET /api/patient-behaviours`). */
export interface PatientBehaviourAssignment {
  /** Synthetic identifier of the patient; always prefixed `SYN-`. */
  patientSyntheticId: string;
  /** How that patient reacts to notifications. */
  behaviour: PatientBehaviour;
  /** The scenario that set the behaviour, or `null` when it was set by hand. */
  scenarioName: string | null;
  /** Simulated-data marker; always `true`. */
  simulated: boolean;
}

/** One FHIR exchange between the simulator and CareLoop (`GET /api/fhir-log`). */
export interface FhirMessageLogEntry {
  /** Simulator-local identifier of the entry. */
  entryId: string;
  /** ISO 8601 time of the exchange. */
  at: string;
  /** `Inbound` when CareLoop called the simulator, `Outbound` for the reverse. */
  direction: 'Inbound' | 'Outbound';
  /** HTTP method of the exchange. */
  method: string;
  /** Request URL or path. */
  url: string;
  /** FHIR resource type exchanged. */
  resourceType: string;
  /** Logical id of the resource, when known. */
  resourceId: string | null;
  /** HTTP status of the response, or `null` when no response arrived. */
  statusCode: number | null;
  /** The FHIR JSON exchanged, when there was a body. */
  body: string | null;
  /** Simulated-data marker; always `true`. */
  simulated: boolean;
}

/**
 * The patient-facing view of a case (`GET /api/case-status/{taskId}`).
 *
 * @remarks
 * Coordination status only, read from the CareLoop case `Task`; it carries no clinical detail.
 */
export interface PatientCaseStatus {
  /** Logical id of the case `Task` in CareLoop. */
  taskId: string;
  /** FHIR `Task.status` code. */
  taskStatus: string;
  /** Pathway stage from `Task.businessStatus`, when present. */
  stage: string | null;
  /** Rejection or closure reason from `Task.statusReason`, when present. */
  statusReason: string | null;
  /** ISO 8601 current deadline from `Task.restriction.period`, when present. */
  dueBy: string | null;
  /** ISO 8601 time CareLoop last changed the task, when known. */
  lastUpdated: string | null;
  /** Simulated-data marker; always `true`. */
  simulated: boolean;
}

/** One scenario as listed by `GET /scenarios`. */
export interface ScenarioSummary {
  /** Scenario key, used to start a run. */
  name: string;
  /** The scenario's own description. */
  description: string;
  /** Identifier of the pathway the scenario runs against. */
  pathwayId: string;
  /** Whether the scenario is a cohort rather than a single journey. */
  isCohort: boolean;
  /** How many patient behaviours a run of this scenario puts in force. */
  patientCount: number;
  /** Simulated-data marker; always `true`. */
  synthetic: boolean;
}

/** Lifecycle of a scenario run. */
export type ScenarioRunStatus = 'Pending' | 'Running' | 'Completed' | 'Failed';

/** One thing the simulator did in reaction to a notification. */
export type PatientReactionKind =
  | 'Delivered'
  | 'DeliveryFailed'
  | 'NoDeliveryReport'
  | 'Acknowledged'
  | 'Understood'
  | 'CallRequested'
  | 'NoResponse'
  | 'ConfidenceRated';

/** A record of one reaction to one notification. */
export interface PatientReactionRecord {
  /** The inbox message the reaction belongs to. */
  messageId: string;
  /** Synthetic identifier of the patient who reacted. */
  patientSyntheticId: string;
  /** What was done. */
  reaction: PatientReactionKind;
  /** Whether the reaction came from the patient's behaviour rather than a click in this app. */
  automatic: boolean;
  /** Deterministic identifier of the emitted `Communication`, or `null` when nothing was sent. */
  sourceEventId: string | null;
  /** ISO 8601 time of the reaction. */
  at: string;
  /** HTTP status CareLoop returned, or `null` when nothing was sent or no response arrived. */
  httpStatusCode: number | null;
  /** Whether the reaction was carried out and, if it sent something, accepted by CareLoop. */
  succeeded: boolean;
  /** Failure detail when `succeeded` is `false`. */
  error: string | null;
}

/** State of a scenario run (`POST /scenarios/{name}/run`, `GET /scenarios/runs/{runId}`). */
export interface ScenarioRunState {
  /** Simulator-local identifier of the run. */
  runId: string;
  /** Name of the scenario whose patient behaviours are in force. */
  scenarioName: string;
  /** Where the run is in its lifecycle. */
  status: ScenarioRunStatus;
  /** ISO 8601 time the behaviours were put in force. */
  startedAt: string;
  /** ISO 8601 completion time, or `null` while the run is in progress. */
  completedAt: string | null;
  /** The patients whose behaviour this run set. */
  patientSyntheticIds: string[];
  /** Every reaction so far to notifications for those patients, oldest first. */
  reactions: PatientReactionRecord[];
  /** Simulated-data marker; always `true`. */
  simulated: boolean;
}
