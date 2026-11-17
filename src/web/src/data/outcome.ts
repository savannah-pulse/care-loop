import type { NotImplemented, Problem } from './types'

/**
 * Why a call to the API did not give the expected answer, in the terms the screens care about.
 *
 * @remarks
 * - `notAvailable`: HTTP 501. The endpoint exists in the contract but is not built yet, so the
 *   screen says so calmly instead of reporting an error.
 * - `notFound`: HTTP 404.
 * - `forbidden`: HTTP 403; the signed-in person may not do this: their role may not, or the case is
 *   outside their team and no reason was given.
 * - `conflict`: HTTP 409; the case changed first. For a handoff summary, AI is off or a summary is
 *   already approved; for an escalation, it was already acknowledged or a newer one replaced it.
 * - `unavailable`: HTTP 503; for a handoff summary, AI could not produce a valid draft; for an AI
 *   agent's words, the agent could not write them this time.
 * - `invalid`: HTTP 400 or 422; the request was refused as malformed.
 * - `aborted`: the read was cancelled because its answer was no longer wanted; screens ignore it.
 * - `error`: anything else, including a network failure.
 */
export type FailureKind =
  | 'notAvailable'
  | 'notFound'
  | 'forbidden'
  | 'conflict'
  | 'unavailable'
  | 'invalid'
  | 'aborted'
  | 'error'

/** A failed call to the API. */
export interface ApiFailure {
  /** What kind of failure it was. */
  readonly kind: FailureKind
  /** The HTTP status, when the API answered at all. */
  readonly status?: number
  /** The explanation the API gave, when it gave one. */
  readonly detail?: string
}

/** The result of one call to the API: the answer, or why there is none. */
export type ApiOutcome<T> =
  | { readonly ok: true; readonly data: T }
  | { readonly ok: false; readonly failure: ApiFailure }

/** What an `openapi-fetch` call resolves to. */
interface FetchResult<T> {
  readonly data?: T
  readonly error?: unknown
  readonly response: Response
}

/**
 * Maps an HTTP status to the kind of failure the screens show.
 *
 * @param status - The HTTP status, or `undefined` when no answer arrived.
 * @returns The failure kind; see {@link FailureKind}.
 */
export function failureKindFor(status: number | undefined): FailureKind {
  switch (status) {
    case 501:
      return 'notAvailable'
    case 404:
      return 'notFound'
    case 403:
      return 'forbidden'
    case 409:
      return 'conflict'
    case 503:
      return 'unavailable'
    case 400:
    case 422:
      return 'invalid'
    default:
      return 'error'
  }
}

function detailOf(body: unknown): string | undefined {
  if (body === null || typeof body !== 'object') {
    return undefined
  }
  const problem = body as Problem & Partial<NotImplemented>
  return problem.detail ?? problem.title ?? problem.message ?? undefined
}

/**
 * Runs one API call and turns its result into an {@link ApiOutcome}.
 *
 * @param request - Starts the call with the typed client and resolves to its result.
 * @returns The data on a successful answer; otherwise the kind of failure, the status and any
 * explanation the API gave. Never rejects: a cancelled call becomes an `aborted` failure and any
 * other failure to reach the API an `error` failure.
 */
export async function callApi<T>(request: () => Promise<FetchResult<T>>): Promise<ApiOutcome<T>> {
  try {
    const { data, error, response } = await request()
    if (response.ok && data !== undefined) {
      return { ok: true, data }
    }
    return {
      ok: false,
      failure: { kind: failureKindFor(response.status), status: response.status, detail: detailOf(error) },
    }
  } catch (error) {
    const aborted = typeof error === 'object' && error !== null && 'name' in error && error.name === 'AbortError'
    return { ok: false, failure: { kind: aborted ? 'aborted' : 'error' } }
  }
}
