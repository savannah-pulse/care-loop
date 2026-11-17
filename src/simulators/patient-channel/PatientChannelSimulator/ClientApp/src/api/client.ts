import i18n from '../i18n';

import type {
  InboxMessage,
  MessageLogEntry,
  PatientBehaviour,
  PatientBehaviourAssignment,
  ScenarioRunState,
  ScenarioSummary,
} from './types';

/**
 * A request to the simulator's .NET host that did not succeed.
 *
 * @remarks
 * `status` is the HTTP status code, or 0 when the host could not be reached at all. The message
 * is readable text for the screen: the host's own problem detail when it sent one.
 */
export class ApiError extends Error {
  /** HTTP status code of the response, or 0 when no response arrived. */
  readonly status: number;

  /**
   * Creates the error.
   *
   * @param status - HTTP status code of the response, or 0 when no response arrived.
   * @param message - Readable description of what went wrong.
   */
  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

/**
 * Turns a failed response into readable text.
 *
 * @param response - The non-2xx response.
 * @returns The host's problem title and detail when it sent them, otherwise a generic message with the status.
 */
async function describeFailure(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { title?: unknown; detail?: unknown };
    const parts = [problem.title, problem.detail].filter(
      (part): part is string => typeof part === 'string' && part.trim().length > 0,
    );
    if (parts.length > 0) {
      return parts.join(' ');
    }
  } catch {
    // Not JSON: fall through to the generic message.
  }

  return i18n.t('errors.status', { status: response.status });
}

/**
 * Sends one request to the simulator's .NET host.
 *
 * @param method - HTTP method.
 * @param path - Path on the host, already URL-encoded.
 * @param body - Value to send as JSON, when the route takes one.
 * @returns The parsed JSON answer, or `undefined` for an empty one.
 * @throws ApiError if the host cannot be reached or answers with a non-2xx status.
 */
async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const init: RequestInit = { method, headers: { Accept: 'application/json' } };
  if (body !== undefined) {
    init.body = JSON.stringify(body);
    init.headers = { ...init.headers, 'Content-Type': 'application/json' };
  }

  let response: Response;
  try {
    response = await fetch(path, init);
  } catch {
    throw new ApiError(0, i18n.t('errors.unreachable'));
  }

  if (!response.ok) {
    throw new ApiError(response.status, await describeFailure(response));
  }

  const text = await response.text();
  if (text.length === 0) {
    return undefined as T;
  }

  try {
    return JSON.parse(text) as T;
  } catch {
    // A 2xx answer that is not JSON is not the host's API (for example a page served in its place).
    throw new ApiError(response.status, i18n.t('errors.notJson'));
  }
}

/**
 * Encodes one path segment.
 *
 * @param value - The raw identifier.
 * @returns The identifier, safe to place between slashes.
 */
function segment(value: string): string {
  return encodeURIComponent(value);
}

/**
 * Lists every simulated text message and email.
 *
 * @returns The messages, newest first.
 * @throws ApiError if the request fails.
 */
export function listInbox(): Promise<InboxMessage[]> {
  return request('GET', '/api/inbox');
}

/**
 * Sends the simulated patient's reply to a text message or an email.
 *
 * @remarks
 * The text goes to CareLoop exactly as typed. The simulator does not say what it means and
 * refuses none for its wording: CareLoop reads 1 as "I got this message" and anything else as a request
 * for a call. This is the only way a reply is created outside a patient's behaviour.
 *
 * @param messageId - Identifier of the message being replied to.
 * @param text - What the patient typed.
 * @returns The updated inbox message.
 * @throws ApiError if the text is empty (400), the message is unknown (404) or cannot be replied to (409), or CareLoop refused or could not be reached (502).
 */
export function replyToMessage(messageId: string, text: string): Promise<InboxMessage> {
  return request('POST', `/api/inbox/${segment(messageId)}/reply`, { text });
}

/**
 * Lists the patients that have a behaviour of their own.
 *
 * @returns One assignment per patient.
 * @throws ApiError if the request fails.
 */
export function listPatientBehaviours(): Promise<PatientBehaviourAssignment[]> {
  return request('GET', '/api/patient-behaviours');
}

/**
 * Reads the behaviour used for patients that have none of their own.
 *
 * @returns The default behaviour.
 * @throws ApiError if the request fails.
 */
export function getDefaultPatientBehaviour(): Promise<PatientBehaviour> {
  return request('GET', '/api/patient-behaviours/default');
}

/**
 * Changes the behaviour used for patients that have none of their own.
 *
 * @param behaviour - The new default.
 * @returns The stored default.
 * @throws ApiError if the behaviour breaks a rule (400).
 */
export function setDefaultPatientBehaviour(behaviour: PatientBehaviour): Promise<PatientBehaviour> {
  return request('PUT', '/api/patient-behaviours/default', behaviour);
}

/**
 * Sets how one patient reacts to notifications received from now on.
 *
 * @param patientSyntheticId - Synthetic identifier of the patient.
 * @param behaviour - The behaviour to put in force.
 * @returns The stored assignment.
 * @throws ApiError if the identifier is not synthetic or the behaviour breaks a rule (400).
 */
export function setPatientBehaviour(
  patientSyntheticId: string,
  behaviour: PatientBehaviour,
): Promise<PatientBehaviourAssignment> {
  return request('PUT', `/api/patient-behaviours/${segment(patientSyntheticId)}`, behaviour);
}

/**
 * Removes a patient's own behaviour so the default applies to them again.
 *
 * @param patientSyntheticId - Synthetic identifier of the patient.
 * @returns Nothing.
 * @throws ApiError if the patient has no behaviour of their own (404).
 */
export async function removePatientBehaviour(patientSyntheticId: string): Promise<void> {
  await request<undefined>('DELETE', `/api/patient-behaviours/${segment(patientSyntheticId)}`);
}

/**
 * Lists the scenarios the simulator can run.
 *
 * @returns The available scenarios.
 * @throws ApiError if the request fails or the scenario files cannot be read (500).
 */
export function listScenarios(): Promise<ScenarioSummary[]> {
  return request('GET', '/scenarios');
}

/**
 * Puts the patient behaviours of a scenario in force, so those patients react to CareLoop's notifications.
 *
 * @param name - Scenario key.
 * @param realSecondsPerScenarioHour - Pacing for this run; the host's configured pacing when omitted.
 * @returns The new run's initial state.
 * @throws ApiError if the scenario is a cohort or the pacing is invalid (400), or the scenario is unknown (404).
 */
export function runScenario(
  name: string,
  realSecondsPerScenarioHour?: number,
): Promise<ScenarioRunState> {
  return request(
    'POST',
    `/scenarios/${segment(name)}/run`,
    realSecondsPerScenarioHour === undefined ? {} : { realSecondsPerScenarioHour },
  );
}

/**
 * Reads the state of a scenario run.
 *
 * @param runId - Identifier returned when the run was started.
 * @returns The run's current state.
 * @throws ApiError if the run is unknown (404).
 */
export function getScenarioRun(runId: string): Promise<ScenarioRunState> {
  return request('GET', `/scenarios/runs/${segment(runId)}`);
}

/**
 * Lists the exchanges between the simulator and CareLoop.
 *
 * @returns The logged exchanges, newest first.
 * @throws ApiError if the request fails.
 */
export function listMessageLog(): Promise<MessageLogEntry[]> {
  return request('GET', '/api/message-log');
}
