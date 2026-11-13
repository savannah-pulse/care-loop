import type {
  FhirMessageLogEntry,
  InboxAction,
  InboxMessage,
  PatientBehaviour,
  PatientBehaviourAssignment,
  PatientCaseStatus,
  ScenarioRunState,
  ScenarioSummary,
} from './types';

/**
 * Builds the rejection every scaffolded client function returns.
 *
 * @param name - Name of the client function that is not implemented yet.
 * @returns A promise rejected with a `WS8` not-implemented error.
 */
function notImplemented(name: string): Promise<never> {
  return Promise.reject(new Error(`WS8: ${name}`));
}

/**
 * Lists the simulated patient inbox.
 *
 * @returns The inbox messages, newest first.
 * @throws Error if the request fails or the route is not implemented.
 */
export function listInbox(): Promise<InboxMessage[]> {
  // TODO(WS-8): fetch GET /api/inbox and return the parsed JSON.
  return notImplemented('listInbox');
}

/**
 * Performs a patient action on an inbox message.
 *
 * @remarks
 * These actions are the only way a patient response is created outside a scenario run.
 *
 * @param messageId - Simulator-local identifier of the message.
 * @param action - The action to perform.
 * @returns The updated inbox message.
 * @throws Error if the message is unknown, CareLoop is unreachable or the route is not implemented.
 */
export function actOnInboxMessage(messageId: string, action: InboxAction): Promise<InboxMessage> {
  // TODO(WS-8): fetch POST /api/inbox/{messageId}/{action} and return the parsed JSON.
  void messageId;
  void action;
  return notImplemented('actOnInboxMessage');
}

/**
 * Reads the patient-facing status of a case.
 *
 * @param taskId - Logical id of the case `Task` in CareLoop.
 * @returns The case status.
 * @throws Error if the task is unknown, CareLoop is unreachable or the route is not implemented.
 */
export function getCaseStatus(taskId: string): Promise<PatientCaseStatus> {
  // TODO(WS-8): fetch GET /api/case-status/{taskId} and return the parsed JSON.
  void taskId;
  return notImplemented('getCaseStatus');
}

/**
 * Lists the patients that have a behaviour of their own.
 *
 * @returns One assignment per patient.
 * @throws Error if the request fails or the route is not implemented.
 */
export function listPatientBehaviours(): Promise<PatientBehaviourAssignment[]> {
  // TODO(WS-8): fetch GET /api/patient-behaviours and return the parsed JSON.
  return notImplemented('listPatientBehaviours');
}

/**
 * Reads the behaviour used for patients that have none of their own.
 *
 * @returns The default behaviour.
 * @throws Error if the request fails or the route is not implemented.
 */
export function getDefaultPatientBehaviour(): Promise<PatientBehaviour> {
  // TODO(WS-8): fetch GET /api/patient-behaviours/default and return the parsed JSON.
  return notImplemented('getDefaultPatientBehaviour');
}

/**
 * Changes the behaviour used for patients that have none of their own.
 *
 * @param behaviour - The new default.
 * @returns The stored default.
 * @throws Error if the behaviour is invalid or the route is not implemented.
 */
export function setDefaultPatientBehaviour(behaviour: PatientBehaviour): Promise<PatientBehaviour> {
  // TODO(WS-8): fetch PUT /api/patient-behaviours/default with the behaviour and return the parsed JSON.
  void behaviour;
  return notImplemented('setDefaultPatientBehaviour');
}

/**
 * Sets how one patient reacts to notifications received from now on.
 *
 * @param patientSyntheticId - Synthetic identifier of the patient.
 * @param behaviour - The behaviour to put in force.
 * @returns The stored assignment.
 * @throws Error if the behaviour is invalid or the route is not implemented.
 */
export function setPatientBehaviour(
  patientSyntheticId: string,
  behaviour: PatientBehaviour,
): Promise<PatientBehaviourAssignment> {
  // TODO(WS-8): fetch PUT /api/patient-behaviours/{patientSyntheticId} with the behaviour and return the parsed JSON.
  void patientSyntheticId;
  void behaviour;
  return notImplemented('setPatientBehaviour');
}

/**
 * Removes a patient's own behaviour so the default applies to them again.
 *
 * @param patientSyntheticId - Synthetic identifier of the patient.
 * @returns Nothing.
 * @throws Error if the patient has no behaviour or the route is not implemented.
 */
export function removePatientBehaviour(patientSyntheticId: string): Promise<void> {
  // TODO(WS-8): fetch DELETE /api/patient-behaviours/{patientSyntheticId}.
  void patientSyntheticId;
  return notImplemented('removePatientBehaviour');
}

/**
 * Lists the scenarios the simulator can run.
 *
 * @returns The available scenarios.
 * @throws Error if the request fails or the route is not implemented.
 */
export function listScenarios(): Promise<ScenarioSummary[]> {
  // TODO(WS-8): fetch GET /scenarios and return the parsed JSON.
  return notImplemented('listScenarios');
}

/**
 * Puts the patient behaviours of a scenario in force, so those patients react to CareLoop's notifications.
 *
 * @param name - Scenario key.
 * @returns The new run's initial state.
 * @throws Error if the scenario is unknown or the route is not implemented.
 */
export function runScenario(name: string): Promise<ScenarioRunState> {
  // TODO(WS-8): fetch POST /scenarios/{name}/run and return the parsed JSON.
  void name;
  return notImplemented('runScenario');
}

/**
 * Reads the state of a scenario run.
 *
 * @param runId - Identifier returned when the run was started.
 * @returns The run's current state.
 * @throws Error if the run is unknown or the route is not implemented.
 */
export function getScenarioRun(runId: string): Promise<ScenarioRunState> {
  // TODO(WS-8): fetch GET /scenarios/runs/{runId} and return the parsed JSON.
  void runId;
  return notImplemented('getScenarioRun');
}

/**
 * Lists the FHIR exchanges between the simulator and CareLoop.
 *
 * @returns The logged exchanges, newest first.
 * @throws Error if the request fails or the route is not implemented.
 */
export function listFhirLog(): Promise<FhirMessageLogEntry[]> {
  // TODO(WS-8): fetch GET /api/fhir-log and return the parsed JSON.
  return notImplemented('listFhirLog');
}
