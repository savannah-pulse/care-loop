import { useCallback } from 'react'
import { api, type CareLoopClient } from './client'
import { callApi, type ApiOutcome } from './outcome'
import { useApiRead, type Read, type ReadOptions } from './read'
import type {
  AiText,
  CaseDetail,
  CaseSummary,
  Metrics,
  Programme,
  SimulationResult,
  TimelineEntry,
} from './types'

/**
 * Most cases `GET /cases` returns in one page (`take`).
 *
 * @remarks
 * The API refuses a page size outside 1 to 200, so longer lists are read page by page.
 */
export const PAGE_SIZE = 200

/**
 * Most cases a list reads, page by page, before it stops.
 *
 * @remarks
 * A team's open cases fit well within it. When a list reaches it, the screen says that only the
 * most recently updated cases are shown.
 */
export const CASE_LIST_LIMIT = 1000

/**
 * Name of the cohort scenario the impact screen replays with and without escalation.
 *
 * @remarks
 * `cohort` is the shared scenario built for this comparison.
 */
export const COMPARISON_COHORT = 'cohort'

/** Which cases a list read asks for. Every field is optional. */
export interface CaseListQuery {
  /** A facility other than the reader's own, which then needs a {@link CaseListQuery.reason}. */
  readonly facilityId?: string
  /** Why the reader needs cases outside their team; recorded by the API with the read. */
  readonly reason?: string
  /** `true` for cases with an open escalation only. */
  readonly escalated?: boolean
  /** One patient's cases only; the API allows this search to clinician roles only. */
  readonly patientReference?: string
}

/** Cases read page by page, and whether every page was read. */
export interface CaseList {
  /** The cases, most recently updated first, each once. */
  readonly cases: readonly CaseSummary[]
  /** `false` when the list stopped at its limit and more cases may exist. */
  readonly complete: boolean
}

/** Settings for {@link loadCaseList}. */
export interface LoadCaseListOptions {
  /** Cancels the read. */
  readonly signal?: AbortSignal
  /** Most cases to read; {@link CASE_LIST_LIMIT} by default. */
  readonly limit?: number
  /** The client to read with; the dashboard's own by default. */
  readonly client?: CareLoopClient
}

/**
 * Turns a reason into the `reason` query parameter.
 *
 * @param reason - The reason given, if any.
 * @returns The reason without surrounding spaces, or `undefined` when there is none, so a blank
 * reason is never sent.
 */
export function reasonParameter(reason: string | undefined): string | undefined {
  const text = reason?.trim()
  return text ? text : undefined
}

/**
 * Reads cases page by page with `skip` and `take`, up to a limit.
 *
 * @param query - Which cases to read.
 * @param options - Cancellation, the limit and the client.
 * @returns Every matching case up to the limit, each once, and whether the list is complete; or the
 * failure of the first page that failed.
 *
 * @remarks
 * Side effect: calls `GET /cases` once per page of {@link PAGE_SIZE} cases, until a page comes back
 * short or the limit is reached.
 */
export async function loadCaseList(
  query: CaseListQuery,
  options: LoadCaseListOptions = {},
): Promise<ApiOutcome<CaseList>> {
  const client = options.client ?? api
  const limit = Math.max(1, Math.floor(options.limit ?? CASE_LIST_LIMIT))
  const filters = {
    facilityId: query.facilityId,
    reason: reasonParameter(query.reason),
    escalated: query.escalated,
    patientReference: query.patientReference,
  }
  const seen = new Set<string>()
  const cases: CaseSummary[] = []
  let skip = 0
  for (;;) {
    const take = Math.min(PAGE_SIZE, limit - skip)
    const page = await callApi(() =>
      client.GET('/cases', { params: { query: { ...filters, skip, take } }, signal: options.signal }),
    )
    if (!page.ok) {
      return page
    }
    for (const row of page.data) {
      if (!seen.has(row.caseId)) {
        seen.add(row.caseId)
        cases.push(row)
      }
    }
    skip += take
    if (page.data.length < take) {
      return { ok: true, data: { cases, complete: true } }
    }
    if (skip >= limit) {
      return { ok: true, data: { cases, complete: false } }
    }
  }
}

/**
 * Reads a list of cases for a screen, page by page.
 *
 * @param query - Which cases to read; by default the open and closed cases of the reader's team.
 * @param options - `{ live: true }` to keep the list up to date while the screen is open, as the
 * worklist does; off by default.
 * @returns The read of the list.
 *
 * @remarks
 * Side effects: calls `GET /cases`, one call per page, on mount, when the query changes and when
 * someone else signs in. With `live`, it polls: it reads again, quietly, every 30 seconds while the
 * page is visible and when the page comes back into view (see `useApiRead`).
 */
export function useCaseList(query: CaseListQuery = {}, options: ReadOptions = {}): Read<CaseList> {
  const { facilityId, reason, escalated, patientReference } = query
  const load = useCallback(
    (signal: AbortSignal) => loadCaseList({ facilityId, reason, escalated, patientReference }, { signal }),
    [facilityId, reason, escalated, patientReference],
  )
  return useApiRead(load, undefined, options)
}

/**
 * Reads one case, and keeps it up to date while the screen is open.
 *
 * @param caseId - CareLoop's identifier of the case.
 * @param reason - Why the reader opens a case outside their team, when they gave one.
 * @returns The read of `GET /cases/{caseId}`. A missing case fails with `notFound`; a case outside
 * the reader's team without a reason, or one their role may not read, fails with `forbidden`.
 *
 * @remarks
 * Side effects: calls the API on mount, when the case or reason changes and when someone else signs
 * in, and polls: it reads again, quietly, every 30 seconds while the page is visible and when the
 * page comes back into view (see `useApiRead`). The API records every read, with the reason.
 */
export function useCase(caseId: string, reason?: string): Read<CaseDetail> {
  const load = useCallback(
    (signal: AbortSignal) =>
      callApi(() =>
        api.GET('/cases/{caseId}', {
          params: { path: { caseId }, query: { reason: reasonParameter(reason) } },
          signal,
        }),
      ),
    [caseId, reason],
  )
  return useApiRead(load, undefined, { live: true })
}

/**
 * Reads the timeline of one case, and keeps it up to date while the screen is open.
 *
 * @param caseId - CareLoop's identifier of the case.
 * @param reason - Why the reader opens a case outside their team, when they gave one.
 * @returns The read of `GET /cases/{caseId}/timeline`, in the order the API sends it.
 *
 * @remarks
 * Side effects: calls the API on mount, when the case or reason changes and when someone else signs
 * in, and polls: it reads again, quietly, every 30 seconds while the page is visible and when the
 * page comes back into view (see `useApiRead`).
 */
export function useCaseTimeline(caseId: string, reason?: string): Read<TimelineEntry[]> {
  const load = useCallback(
    (signal: AbortSignal) =>
      callApi(() =>
        api.GET('/cases/{caseId}/timeline', {
          params: { path: { caseId }, query: { reason: reasonParameter(reason) } },
          signal,
        }),
      ),
    [caseId, reason],
  )
  return useApiRead(load, undefined, { live: true })
}

/**
 * Reads a patient's other cases, for clinicians.
 *
 * @param patientReference - The patient's source-system reference.
 * @param facilityId - The facility of the case being looked at.
 * @param reason - The reason given for that case, when it is outside the reader's team.
 * @returns The read of `GET /cases?patientReference=…`, in the case's facility.
 *
 * @remarks
 * Side effects: calls the API on mount, when the arguments change and when someone else signs in;
 * it does not poll. The API allows this search to clinician roles only and records it.
 */
export function usePatientCases(patientReference: string, facilityId: string, reason?: string): Read<CaseList> {
  return useCaseList({ patientReference, facilityId: reason ? facilityId : undefined, reason })
}

/**
 * Reads the current loop-closure metrics of the reader's team, or of every facility for the
 * programme coordinator, and keeps them up to date while the screen is open.
 *
 * @returns The read of `GET /metrics`.
 *
 * @remarks
 * Side effects: calls the API on mount and when someone else signs in, and polls: it reads again,
 * quietly, every 30 seconds while the page is visible and when the page comes back into view (see
 * `useApiRead`).
 */
export function useMetrics(): Read<Metrics> {
  const load = useCallback(
    (signal: AbortSignal) => callApi(() => api.GET('/metrics', { signal })),
    [],
  )
  return useApiRead(load, undefined, { live: true })
}

/**
 * Runs the with-and-without-escalation comparison on a cohort scenario.
 *
 * @param cohortScenario - Name of the cohort scenario to replay; {@link COMPARISON_COHORT} by default.
 * @returns The read of `POST /metrics/simulation`, using the scenario's own seed.
 *
 * @remarks
 * Side effects: posts to the API on mount, on `retry` and when someone else signs in; it never
 * polls, because each post replays a whole cohort. The comparison replays the cohort in isolation
 * and changes no case, so it is safe to repeat.
 */
export function useSimulation(cohortScenario: string = COMPARISON_COHORT): Read<SimulationResult> {
  const load = useCallback(
    (signal: AbortSignal) =>
      callApi(() => api.POST('/metrics/simulation', { body: { cohortScenario, seed: null }, signal })),
    [cohortScenario],
  )
  return useApiRead(load)
}

/**
 * Reads every screening programme's settings.
 *
 * @returns The read of `GET /programmes`.
 *
 * @remarks
 * Side effects: calls the API on mount and when someone else signs in.
 */
export function useProgrammes(): Read<Programme[]> {
  const load = useCallback(
    (signal: AbortSignal) => callApi(() => api.GET('/programmes', { signal })),
    [],
  )
  return useApiRead(load)
}

/**
 * Reads one screening programme's settings.
 *
 * @param pathwayId - The programme's pathway identifier, for example `breast-screening`.
 * @returns The read of `GET /programmes/{pathwayId}`; an unknown programme fails with `notFound`.
 *
 * @remarks
 * Side effects: calls the API on mount, when the programme changes and when someone else signs in.
 */
export function useProgramme(pathwayId: string): Read<Programme> {
  const load = useCallback(
    (signal: AbortSignal) =>
      callApi(() => api.GET('/programmes/{pathwayId}', { params: { path: { pathwayId } }, signal })),
    [pathwayId],
  )
  return useApiRead(load)
}

/**
 * Reads one case in a single call, outside React.
 *
 * @param caseId - The case.
 * @param reason - The reason given for a case outside the reader's team.
 * @param signal - Cancels the read.
 * @param client - The client to read with; the dashboard's own by default.
 * @returns The case, or why it could not be read.
 */
export function readCase(
  caseId: string,
  reason?: string,
  signal?: AbortSignal,
  client: CareLoopClient = api,
): Promise<ApiOutcome<CaseDetail>> {
  return callApi(() =>
    client.GET('/cases/{caseId}', {
      params: { path: { caseId }, query: { reason: reasonParameter(reason) } },
      signal,
    }),
  )
}

/**
 * Reads the AI-written summary of the signed-in person's open cases.
 *
 * @param refreshes - How many times the reader has asked for a new summary. Above zero, the API is
 * asked to write a new one instead of reusing the one it kept.
 * @returns The summary, or why there is none: `conflict` while AI is switched off, `unavailable`
 * when AI could not write it.
 *
 * @remarks
 * Side effect: calls `GET /ai/role-briefing`, once per reader and once more per refresh. It is not
 * re-read on a timer: the API reuses its answer while the cases are unchanged.
 */
export function useRoleBriefing(refreshes: number = 0): Read<AiText> {
  const load = useCallback(
    (signal: AbortSignal) =>
      callApi(() =>
        api.GET('/ai/role-briefing', { params: { query: { refresh: refreshes > 0 ? true : undefined } }, signal }),
      ),
    [refreshes],
  )
  return useApiRead(load)
}

/**
 * Reads the AI-written reading of the impact figures.
 *
 * @param refreshes - How many times the reader has asked for a new summary. Above zero, the API is
 * asked to write a new one instead of reusing the one it kept.
 * @returns The summary, or why there is none: `conflict` while AI is switched off, `unavailable`
 * when AI could not write it.
 *
 * @remarks
 * Side effect: calls `GET /ai/impact-summary`, once per reader and once more per refresh, for the
 * same scope as `GET /metrics`: the reader's facility, or every facility for the coordinator.
 */
export function useImpactSummary(refreshes: number = 0): Read<AiText> {
  const load = useCallback(
    (signal: AbortSignal) =>
      callApi(() =>
        api.GET('/ai/impact-summary', { params: { query: { refresh: refreshes > 0 ? true : undefined } }, signal }),
      ),
    [refreshes],
  )
  return useApiRead(load)
}

