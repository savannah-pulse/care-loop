import type { components } from './schema'
import { t } from '../i18n'

/** An API case-list row; generated from the OpenAPI contract. */
export type CaseSummary = components['schemas']['CaseSummaryResponse']
/** Full case coordination state; generated from the OpenAPI contract. */
export type CaseDetail = components['schemas']['CaseDetailResponse']
/** An unresolved case exception; generated from the OpenAPI contract. */
export type ExceptionItem = components['schemas']['ExceptionItemResponse']
/** A received-order audit entry; generated from the OpenAPI contract. */
export type TimelineEntry = components['schemas']['TimelineEntryResponse']
/** Current coordination measures; generated from the OpenAPI contract. */
export type Metrics = components['schemas']['MetricsResponse']
/** A read-only pathway definition; generated from the OpenAPI contract. */
export type Programme = components['schemas']['ProgrammeResponse']
/** A source-cited handoff draft and its review state. */
export type HandoffSummary = components['schemas']['HandoffSummaryResponse']

/** Caller-stated prototype identity; this is not production authentication. */
export interface StaffIdentity {
  /** The staff reference sent on each request. */
  user: string
  /** A configured human role. */
  role: string
  /** The reader's facility, the current team boundary. */
  facility: string
}

/** Optional read filters, encoded as query parameters. */
export type Query = Record<string, string | number | boolean | undefined>

/** A refused API request, localized by HTTP status rather than exposing internal server errors. */
export class ApiError extends Error {
  /** The response's HTTP status. */
  readonly status: number

  /**
   * Creates a localized request failure.
   * @param status - The server's refusal status.
   */
  constructor(status: number) {
    const messages: Record<number, string> = {
      401: t('error.401'), 403: t('error.403'), 404: t('error.404'),
      409: t('error.409'), 501: t('error.501'), 503: t('error.503'),
    }
    super(messages[status] ?? t('error.request', { status }))
    this.status = status
  }
}

/**
 * Encodes defined query values, including zero and false.
 * @param query - The request filters.
 * @returns An encoded query suffix, or an empty string.
 */
export function queryString(query: Query): string {
  const values = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== '') values.set(key, String(value))
  }
  return values.size ? `?${values}` : ''
}

/**
 * Tells whether a value can be sent as an HTTP header value.
 * @param value - Text destined for a request header.
 * @returns `false` for control characters and anything above U+00FF, which `fetch` refuses to send.
 * @remarks The API reads the identity headers as plain text and does not decode percent-encoding, so a value that
 * cannot be carried is refused rather than silently encoded into a different identity.
 */
export function isHeaderSafe(value: string): boolean {
  return /^[\u0020-\u007e\u00a0-\u00ff]*$/.test(value)
}

/** Typed operations at the CareLoop REST boundary. */
export interface CareLoopApi {
  /** Reads a case page with staff identity and optional access reason. */
  cases(query: Query, signal?: AbortSignal): Promise<CaseSummary[]>
  /** Reads the unresolved exception queue. */
  exceptions(query: Query, signal?: AbortSignal): Promise<ExceptionItem[]>
  /** Reads full case coordination state. */
  detail(id: string, reason?: string, signal?: AbortSignal): Promise<CaseDetail>
  /** Reads the case audit timeline. */
  timeline(id: string, reason?: string, signal?: AbortSignal): Promise<TimelineEntry[]>
  /** Reads current coordination measures. */
  metrics(signal?: AbortSignal): Promise<Metrics>
  /** Lists current pathway definitions. */
  programmes(signal?: AbortSignal): Promise<Programme[]>
  /** Reads one current pathway definition. */
  programme(id: string, signal?: AbortSignal): Promise<Programme>
  /** Records a human seeing a specific escalation, without changing accountability. */
  acknowledge(caseId: string, escalationId: string, reason?: string): Promise<CaseDetail>
  /** Requests an AI draft; it may explicitly refuse when WS9 or AI is unavailable. */
  draft(caseId: string): Promise<HandoffSummary>
  /** Approves exactly the text a clinician checked. */
  approve(caseId: string, summaryId: string, text: string): Promise<HandoffSummary>
  /** Rejects a draft with a documented reason. */
  reject(caseId: string, summaryId: string, reason: string): Promise<HandoffSummary>
}

/**
 * Creates a typed client using the same-origin proxy and identity on every request.
 * @param identity - The current staff identity.
 * @returns REST operations; reads can be cancelled and all failed responses throw.
 */
export function createApi(identity: StaffIdentity): CareLoopApi {
  async function request<T>(path: string, signal?: AbortSignal, body?: object): Promise<T> {
    if (![identity.user, identity.role, identity.facility].every(isHeaderSafe)) throw new Error(t('error.identityHeader'))
    let response: Response
    try {
      response = await fetch(`/api${path}`, {
        signal,
        method: body === undefined ? 'GET' : 'POST',
        headers: {
          'X-Demo-User': identity.user,
          'X-Demo-Role': identity.role,
          'X-Demo-Facility': identity.facility,
          ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
        },
        body: body === undefined ? undefined : JSON.stringify(body),
      })
    } catch (error) {
      if (signal?.aborted) throw error
      throw new Error(t('error.network'), { cause: error })
    }
    if (!response.ok) throw new ApiError(response.status)
    try {
      return await response.json() as T
    } catch (error) {
      if (signal?.aborted) throw error
      throw new Error(t('error.invalidResponse'), { cause: error })
    }
  }
  const casePath = (id: string) => `/cases/${encodeURIComponent(id)}`
  return {
    cases: (query, signal) => request(`/cases${queryString(query)}`, signal),
    exceptions: (query, signal) => request(`/exceptions${queryString(query)}`, signal),
    detail: (id, reason, signal) => request(`${casePath(id)}${queryString({ reason })}`, signal),
    timeline: (id, reason, signal) => request(`${casePath(id)}/timeline${queryString({ reason })}`, signal),
    metrics: (signal) => request('/metrics', signal),
    programmes: (signal) => request('/programmes', signal),
    programme: (id, signal) => request(`/programmes/${encodeURIComponent(id)}`, signal),
    acknowledge: (caseId, escalationId, reason) =>
      request(`${casePath(caseId)}/escalations/${encodeURIComponent(escalationId)}/acknowledgement${queryString({ reason })}`, undefined, {}),
    draft: (caseId) => request(`${casePath(caseId)}/handoff-summary`, undefined, {}),
    approve: (caseId, summaryId, text) =>
      request(`${casePath(caseId)}/handoff-summary/${encodeURIComponent(summaryId)}/approval`, undefined, { approvedText: text }),
    reject: (caseId, summaryId, reason) =>
      request(`${casePath(caseId)}/handoff-summary/${encodeURIComponent(summaryId)}/rejection`, undefined, { reason }),
  }
}
