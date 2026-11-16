/**
 * Hand-written types mirroring the JSON contracts of the SMS and email simulator's .NET host.
 *
 * @remarks
 * Every record is synthetic and carries a `simulated` (or `synthetic`) marker that the UI must show.
 */

/**
 * The channel a message arrived on.
 *
 * @remarks
 * CareLoop reaches people by text message and email only: there is no health app or patient portal.
 */
export type InboxChannel = 'Sms' | 'Email';

/**
 * Who a message is for.
 *
 * @remarks
 * Only a message to the patient can be replied to: nothing a caregiver or a member of staff does
 * may count as the patient's reply.
 */
export type RecipientKind = 'Patient' | 'Caregiver' | 'Staff';

/**
 * What the simulator reported to CareLoop about delivery.
 *
 * @remarks
 * Delivery is a transport fact only; a delivered message is not an acknowledgement.
 */
export type DeliveryState = 'Pending' | 'Delivered' | 'Failed' | 'Silent';

/** One reply to a message, as the simulated patient typed it. */
export interface InboxReply {
  /** The simulator's identifier of the reply. */
  replyId: string;
  /** The text exactly as typed. The simulator does not say what it means: CareLoop decides. */
  text: string;
  /** ISO 8601 time CareLoop accepted the reply. */
  sentAt: string;
}

/** One simulated text message or email (`GET /api/inbox`). */
export interface InboxMessage {
  /** The identifier CareLoop gave the message. */
  messageId: string;
  /** The case the message is about, when its metadata named one. */
  caseId: string | null;
  /** The synthetic phone number or email address it was sent to (prefixed `SYN-`). */
  to: string;
  /** Visibly synthetic display name of whoever it is for (starts with "Synthetic"). */
  displayName: string;
  /** Whether it is a text message or an email. */
  channel: InboxChannel;
  /** The subject line of an email; `null` for a text message. */
  subject: string | null;
  /** BCP 47 language tag of the message content. */
  language: string;
  /** Message text exactly as CareLoop rendered it. */
  body: string;
  /** ISO 8601 time the simulator received the message. */
  receivedAt: string;
  /** Delivery outcome reported to CareLoop. */
  delivery: DeliveryState;
  /** Reason reported with a failed delivery, otherwise `null`. */
  deliveryFailureReason: string | null;
  /** Whether the message is for the patient, a caregiver or a member of staff. */
  recipientKind: RecipientKind;
  /** The replies CareLoop accepted for this message, oldest first. */
  replies: InboxReply[];
  /** Simulated-data marker; always `true`. */
  simulated: boolean;
}

/** What the channel reports when a notification for a patient arrives. */
export type DeliveryBehaviour = 'Deliver' | 'Fail' | 'StaySilent';

/**
 * What a patient does, on their own, after a notification has been delivered to them.
 *
 * @remarks
 * `Acknowledge` replies 1 ("I will come") and `CallMe` replies 2. A patient cannot say the next
 * step back or rate their confidence by text or email: a nurse records those after a call.
 */
export type PatientResponseBehaviour = 'None' | 'Acknowledge' | 'CallMe';

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
}

/** The behaviour in force for one patient (`GET /api/patient-behaviours`). */
export interface PatientBehaviourAssignment {
  /** Synthetic identifier of the patient, which is also the address their messages are sent to; always prefixed `SYN-`. */
  patientSyntheticId: string;
  /** How that patient reacts to notifications. */
  behaviour: PatientBehaviour;
  /** The scenario that set the behaviour, or `null` when it was set by hand. */
  scenarioName: string | null;
  /** Simulated-data marker; always `true`. */
  simulated: boolean;
}

/** One exchange between the simulator and CareLoop (`GET /api/message-log`). */
export interface MessageLogEntry {
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
  /** What was exchanged: `message`, `delivery` or `reply`. */
  kind: string;
  /** The message the exchange is about, when known. */
  messageId: string | null;
  /** HTTP status of the response, or `null` when no response arrived. */
  statusCode: number | null;
  /** The JSON that was sent, when it was kept. */
  body: string | null;
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
  /** Why the scenario file cannot be used, otherwise `null`. A scenario with a problem cannot be run. */
  problem: string | null;
}

/** Lifecycle of a scenario run. */
export type ScenarioRunStatus = 'Pending' | 'Running' | 'Completed' | 'Failed';

/** One thing the simulator did in reaction to a notification. */
export type PatientReactionKind =
  'Delivered' | 'DeliveryFailed' | 'NoDeliveryReport' | 'Replied' | 'NoResponse';

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
  /** Identifier of the callback that was sent (the reply, or the message with its delivery status), or `null` when nothing was sent. */
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
