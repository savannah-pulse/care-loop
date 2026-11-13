import type { HealthResponse } from './types';

// TODO(WS-7): add typed functions for GET /scenarios, POST /scenarios/{name}/run,
// GET /scenarios/runs/{runId}, GET /staff-inbox, GET /fhir-log, GET /cases and POST /actions/*.

/**
 * Reads the simulator's liveness endpoint.
 *
 * @param signal - Optional abort signal, for example from a component that unmounts.
 * @returns The parsed `GET /health` body.
 * @throws Error when the simulator answers with a non-success status.
 *
 * @example
 * ```ts
 * const health = await fetchHealth();
 * console.log(health.status); // "ok"
 * ```
 */
export async function fetchHealth(signal?: AbortSignal): Promise<HealthResponse> {
  const response = await fetch('/health', { signal: signal ?? null });
  if (!response.ok) {
    throw new Error(`GET /health failed with status ${String(response.status)}`);
  }
  return (await response.json()) as HealthResponse;
}
