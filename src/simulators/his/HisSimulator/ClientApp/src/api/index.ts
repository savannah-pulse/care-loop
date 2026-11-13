/**
 * HTTP access to the HIS simulator's own API (not CareLoop's): scenario runs, staff inbox,
 * FHIR message log and manual clinician actions. Only the health call exists so far.
 */
export { fetchHealth } from './client';
export type { HealthResponse } from './types';
