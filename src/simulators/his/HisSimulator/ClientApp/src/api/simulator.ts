/** A human actor supplied by the simulated hospital, never inferred by CareLoop. */
export interface ActingStaff {
  /** Synthetic source identifier. */
  syntheticId: string;
  /** Source role used to authorize the transition. */
  role: string;
  /** Synthetic facility boundary. */
  facility: string | null;
}
/** A source case, not CareLoop's clinical or coordination projection. */
export interface SimulatedCase {
  /** Local case correlation key. */
  caseRef: string;
  /** Synthetic demographic data held by the HIS. */
  patient: {
    /** Source patient identifier. */
    syntheticId: string;
    /** Clearly synthetic patient display name. */
    displayName: string;
    /** Synthetic medical record number shown to staff next to the case reference. */
    mrn: string;
  };
  /** Configured pathway. */
  pathwayId: string;
  /** Actual CareLoop Task id, once received through FHIR. */
  careLoopTaskId: string | null;
  /** Successfully sent resource references available as evidence. */
  resourceIds: Record<string, string>;
  /** Approved handoff summary exactly as CareLoop sent it, or null when none has been received for the case. */
  approvedHandoffSummary: string | null;
}
/** A validated scenario and its declared, not verified, expectations. */
export interface Scenario {
  /** Scenario file key. */
  name: string;
  /** Source-authored explanation. */
  description: string;
  /** Cohorts are deferred and cannot run yet. */
  isCohort: boolean;
  /** HIS actions in file order. */
  hisStepCount: number;
  /** Expectations are assertions for future journey tests, not run results. */
  expect: Record<string, unknown> | null;
}
/** Progress of a supervised replay; completion is not completed care. */
export interface ScenarioRun {
  /** Identifier for polling. */
  runId: string;
  /** Overall replay state. */
  status: 'pending' | 'running' | 'completed' | 'failed';
  /** Per-step send outcomes. */
  steps: {
    /** Source step key. */
    stepId: string;
    /** A refused event remains distinct from a transport failure. */
    status: string;
    /** Real-time due instant. */
    scheduledAt: string;
    /** Returned upstream statuses, including refusals. */
    httpStatusCodes: number[];
    /** Explicit fatal failure, when present. */
    error: string | null;
  }[];
}
/** A delivered staff notice; reading it never accepts ownership. */
export interface InboxItem {
  /** Stable inbox key on FHIR replay. */
  id: string;
  /** Notification or handoff category. */
  kind: string;
  /** Real received instant. */
  receivedAt: string;
  /** Staff source identifier or queue. */
  recipientSyntheticId: string | null;
  /** Staff-safe source text. */
  subject: string;
  /** Corresponding received FHIR log entry. */
  logEntryId: string | null;
}
/** Exact synthetic wire exchange, including upstream rejection bodies. */
export interface LogEntry {
  /** Log key. */
  id: string;
  /** Relative to this simulator. */
  direction: 'sent' | 'received';
  /** Real exchange instant. */
  at: string;
  /** HTTP verb. */
  method: string;
  /** FHIR resource type. */
  resourceType: string;
  /** HTTP result; null denotes no received answer. */
  statusCode: number | null;
  /** Source-local case correlation when known. */
  caseRef: string | null;
  /** Exact request or received body. */
  body: string;
  /** Exact upstream answer if received. */
  responseBody: string | null;
}
/** Result of sending a human-authored action; a clinical refusal is not an HTTP client failure. */
export interface ActionResult {
  /** True only when every sent resource was accepted. */
  accepted: boolean;
  /** Individual FHIR answers. */
  messages: {
    /** Resource sent. */
    resourceType: string;
    /** Returned HTTP status. */
    statusCode: number;
  }[];
}
/** Explicit local HTTP error, rendered through the locale catalogue. */
export class SimulatorApiError extends Error {
  /** HTTP status for localized error reporting. */
  readonly status: number;
  /**
   * Retains a refused local request's status.
   * @param status - HTTP status returned by the simulator.
   */
  constructor(status: number) {
    super(String(status));
    this.status = status;
  }
}
/**
 * Calls only the simulator's own API.
 * @param baseUrl - Optional origin/base path override.
 * @param path - Simulator-relative route, including any query.
 * @param signal - Cancellation on navigation or changed filters.
 * @param body - Optional JSON request; presence selects POST.
 * @returns The typed simulator response.
 * @throws SimulatorApiError for non-success HTTP results.
 */
export async function simulatorRequest<T>(
  baseUrl: string | undefined,
  path: string,
  signal: AbortSignal,
  body?: unknown,
): Promise<T> {
  const response = await fetch((baseUrl ?? '').replace(/\/$/, '') + path, {
    method: body === undefined ? 'GET' : 'POST',
    signal,
    ...(body === undefined
      ? {}
      : { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  });
  if (!response.ok) throw new SimulatorApiError(response.status);
  return (await response.json()) as T;
}
