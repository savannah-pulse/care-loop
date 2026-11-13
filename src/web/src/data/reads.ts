import { useCallback } from 'react'
import { api } from './client'
import { callApi } from './outcome'
import { useApiRead, type Read } from './read'
import type { CaseDetail, CaseSummary, Metrics, SimulationResult, TimelineEntry } from './types'

/**
 * Most cases the worklist asks for in one call (`take`).
 *
 * @remarks
 * The worklist shows every open case of the signed-in person's team. Limiting the list to their
 * team is C4 and paging is not built, so one large page is requested.
 */
export const CASE_LIST_LIMIT = 500

/**
 * Name of the cohort scenario the impact screen replays with and without escalation.
 *
 * @remarks
 * `cohort` is the shared scenario built for this comparison.
 */
export const COMPARISON_COHORT = 'cohort'

/**
 * Reads the case list for the worklist.
 *
 * @returns The read of `GET /cases`, asking for up to {@link CASE_LIST_LIMIT} cases.
 *
 * @remarks
 * Side effects: calls `GET /cases` on mount and again when someone else signs in.
 */
export function useCases(): Read<CaseSummary[]> {
  const load = useCallback(
    (signal: AbortSignal) =>
      callApi(() => api.GET('/cases', { params: { query: { take: CASE_LIST_LIMIT } }, signal })),
    [],
  )
  return useApiRead(load)
}

/**
 * Reads one case.
 *
 * @param caseId - CareLoop's identifier of the case.
 * @returns The read of `GET /cases/{caseId}`; a missing case fails with `notFound`.
 *
 * @remarks
 * Side effects: calls the API on mount, when `caseId` changes and when someone else signs in.
 */
export function useCase(caseId: string): Read<CaseDetail> {
  const load = useCallback(
    (signal: AbortSignal) =>
      callApi(() => api.GET('/cases/{caseId}', { params: { path: { caseId } }, signal })),
    [caseId],
  )
  return useApiRead(load)
}

/**
 * Reads the timeline of one case.
 *
 * @param caseId - CareLoop's identifier of the case.
 * @returns The read of `GET /cases/{caseId}/timeline`, in the order the API sends it.
 *
 * @remarks
 * Side effects: calls the API on mount, when `caseId` changes and when someone else signs in.
 */
export function useCaseTimeline(caseId: string): Read<TimelineEntry[]> {
  const load = useCallback(
    (signal: AbortSignal) =>
      callApi(() => api.GET('/cases/{caseId}/timeline', { params: { path: { caseId } }, signal })),
    [caseId],
  )
  return useApiRead(load)
}

/**
 * Reads the current loop-closure metrics.
 *
 * @returns The read of `GET /metrics`.
 *
 * @remarks
 * Side effects: calls the API on mount and when someone else signs in.
 */
export function useMetrics(): Read<Metrics> {
  const load = useCallback(
    (signal: AbortSignal) => callApi(() => api.GET('/metrics', { signal })),
    [],
  )
  return useApiRead(load)
}

/**
 * Runs the with-and-without-escalation comparison on a cohort scenario.
 *
 * @param cohortScenario - Name of the cohort scenario to replay; {@link COMPARISON_COHORT} by default.
 * @returns The read of `POST /metrics/simulation`, using the scenario's own seed.
 *
 * @remarks
 * Side effects: posts to the API on mount and when someone else signs in. The comparison replays
 * the cohort in isolation and changes no case, so it is safe to repeat.
 */
export function useSimulation(cohortScenario: string = COMPARISON_COHORT): Read<SimulationResult> {
  const load = useCallback(
    (signal: AbortSignal) =>
      callApi(() => api.POST('/metrics/simulation', { body: { cohortScenario, seed: null }, signal })),
    [cohortScenario],
  )
  return useApiRead(load)
}
