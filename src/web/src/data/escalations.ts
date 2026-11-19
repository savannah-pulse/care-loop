import { useCallback, useSyncExternalStore } from 'react'
import { api, type CareLoopClient } from './client'
import { callApi, type ApiOutcome } from './outcome'
import { useApiRead, type Read } from './read'
import { PAGE_SIZE, loadCaseList, readCase, reasonParameter } from './reads'
import type { CaseDetail, CaseSummary } from './types'

/**
 * Records that the signed-in person has seen an escalation.
 *
 * @param caseId - The case the escalation is on.
 * @param escalationId - Exactly the escalation the person saw.
 * @param reason - The reason given for a case outside the person's team, when there is one.
 * @param client - The client to call with; the dashboard's own by default.
 * @returns The case as it now stands. Fails with `forbidden` when the person's role is not the one
 * the escalation went to, or the case is outside their team without a reason; with `conflict` when
 * it was already acknowledged or a newer escalation replaced it.
 *
 * @remarks
 * Side effect: posts to `POST /cases/{caseId}/escalations/{escalationId}/acknowledgement` with no
 * body. Acknowledging never changes who is responsible, clears an exception or stops the clock.
 */
export function acknowledgeEscalation(
  caseId: string,
  escalationId: string,
  reason?: string,
  client: CareLoopClient = api,
): Promise<ApiOutcome<CaseDetail>> {
  return callApi(() =>
    client.POST('/cases/{caseId}/escalations/{escalationId}/acknowledgement', {
      params: { path: { caseId, escalationId }, query: { reason: reasonParameter(reason) } },
    }),
  )
}

let escalationsVersion = 0
const listeners = new Set<() => void>()

/**
 * Tells every screen that shows escalations to read them again, for example after one is
 * acknowledged.
 */
export function escalationsChanged(): void {
  escalationsVersion += 1
  for (const listener of listeners) {
    listener()
  }
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

function version(): number {
  return escalationsVersion
}

/**
 * Follows how many times escalations have changed in this session.
 *
 * @returns A number that goes up each time {@link escalationsChanged} is called.
 */
export function useEscalationsVersion(): number {
  return useSyncExternalStore(subscribe, version)
}

/**
 * Reads how many open escalations the signed-in person's team has, for the navigation, and keeps the
 * number up to date.
 *
 * @returns The read of the number of cases with an open escalation, up to {@link PAGE_SIZE}.
 *
 * @remarks
 * Side effects: calls `GET /cases?escalated=true` on mount, when someone else signs in and after an
 * escalation is acknowledged, and polls: it reads again, quietly, every 30 seconds while the page is
 * visible and when the page comes back into view (see `useApiRead`). The facility lead sees their
 * facility's escalated cases; the programme coordinator sees the cases escalated to them.
 */
export function useEscalationCount(): Read<number> {
  const changes = useEscalationsVersion()
  const load = useCallback(async (signal: AbortSignal): Promise<ApiOutcome<number>> => {
    const outcome = await loadCaseList({ escalated: true }, { signal, limit: PAGE_SIZE })
    return outcome.ok ? { ok: true, data: outcome.data.cases.length } : outcome
  }, [])
  return useApiRead(load, changes, { live: true })
}

/** One escalated case: its list row and, when it could be read, the full case with its escalations. */
export interface EscalatedCase {
  /** The case as `GET /cases` returned it. */
  readonly summary: CaseSummary
  /** The full case, from `GET /cases/{caseId}`; absent when it could not be read. */
  readonly detail?: CaseDetail
}

/** How many cases {@link loadEscalatedCases} reads at once. */
export const DETAIL_READS_AT_ONCE = 4

/**
 * Runs a task for each item, a few at a time, keeping the items' order.
 *
 * @param items - The items.
 * @param limit - How many tasks may run at once.
 * @param task - The task to run for one item.
 * @returns Each task's result, in the order of the items.
 */
export async function mapAtMost<T, R>(
  items: readonly T[],
  limit: number,
  task: (item: T) => Promise<R>,
): Promise<R[]> {
  const results: R[] = new Array<R>(items.length)
  let next = 0
  const worker = async (): Promise<void> => {
    while (next < items.length) {
      const index = next
      next += 1
      results[index] = await task(items[index])
    }
  }
  await Promise.all(Array.from({ length: Math.min(Math.max(1, limit), items.length) }, worker))
  return results
}

/**
 * Reads every case with an open escalation, then each case in full for its escalation ladder.
 *
 * @param signal - Cancels the reads.
 * @param client - The client to read with; the dashboard's own by default.
 * @returns The escalated cases, most recently updated first. A case that could not be read in full
 * keeps its list row only.
 *
 * @remarks
 * Side effects: calls `GET /cases?escalated=true`, page by page, then `GET /cases/{caseId}` for each
 * case, {@link DETAIL_READS_AT_ONCE} at a time.
 */
export async function loadEscalatedCases(
  signal?: AbortSignal,
  client: CareLoopClient = api,
): Promise<ApiOutcome<EscalatedCase[]>> {
  const list = await loadCaseList({ escalated: true }, { signal, client })
  if (!list.ok) {
    return list
  }
  const rows = await mapAtMost(list.data.cases, DETAIL_READS_AT_ONCE, async (summary) => {
    const detail = await readCase(summary.caseId, undefined, signal, client)
    return detail.ok ? { summary, detail: detail.data } : { summary }
  })
  if (signal?.aborted) {
    return { ok: false, failure: { kind: 'aborted' } }
  }
  return { ok: true, data: rows }
}

/**
 * Reads the escalated cases for the Escalations screen, and keeps them up to date.
 *
 * @returns The read of {@link loadEscalatedCases}.
 *
 * @remarks
 * Side effects: reads on mount, when someone else signs in and after an escalation is acknowledged,
 * and polls: it reads again, quietly, every 30 seconds while the page is visible and when the page
 * comes back into view (see `useApiRead`).
 */
export function useEscalatedCases(): Read<EscalatedCase[]> {
  const changes = useEscalationsVersion()
  const load = useCallback((signal: AbortSignal) => loadEscalatedCases(signal), [])
  return useApiRead(load, changes, { live: true })
}
