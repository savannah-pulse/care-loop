/**
 * HTTP access to the HIS simulator's own API (not CareLoop's): scenario runs, staff inbox,
 * FHIR message log and manual clinician actions, with cancellable reads and explicit failures.
 */
export { fetchHealth } from './client';
export type { HealthResponse } from './types';
export { simulatorRequest, SimulatorApiError } from './simulator';
export { useSimulatorRead } from './useSimulatorRead';
