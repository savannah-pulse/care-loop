import type { Tone } from '../../domain'
import { isSameDay } from '../../format'
import type { StaffMember } from '../../identity'
import { isMine, isOverdue, type WorklistRow } from './worklist'

/** Most cases the "Start here today" card lists. */
export const START_HERE_LIMIT = 3

/**
 * Why a case is on the "Start here today" card, from most to least pressing.
 *
 * @remarks
 * - `overdue`: its next action is past its due time.
 * - `escalated`: an escalation is open on it.
 * - `urgent`: the clinician reviewed the result as urgent.
 * - `dueToday`: its next action is due later today in Abu Dhabi.
 */
export type StartReason = 'overdue' | 'escalated' | 'urgent' | 'dueToday'

/** The colour of each reason's dot, always shown with its word. */
export const START_REASON_TONES: Readonly<Record<StartReason, Tone>> = {
  overdue: 'red',
  escalated: 'grey',
  urgent: 'red',
  dueToday: 'blue',
}

/** The order of the card: overdue or escalated, then urgent, then due today. */
const START_ORDER: Readonly<Record<StartReason, number>> = { overdue: 0, escalated: 0, urgent: 1, dueToday: 2 }

/** One case on the "Start here today" card and why it is there. */
export interface StartHereItem {
  /** The case, as the worklist prepared it. */
  readonly row: WorklistRow
  /** Why it is on the card. */
  readonly reason: StartReason
}

/**
 * Works out why a case belongs on the "Start here today" card, if it does.
 *
 * @param row - A worklist row.
 * @param now - The current time.
 * @returns The most pressing reason that applies, or `undefined` when the case is not overdue,
 * escalated, urgent or due today.
 */
export function startReason(row: WorklistRow, now: Date): StartReason | undefined {
  if (isOverdue(row, now)) {
    return 'overdue'
  }
  if (row.summary.isEscalated) {
    return 'escalated'
  }
  if (row.urgent) {
    return 'urgent'
  }
  return row.due && isSameDay(row.due, now) ? 'dueToday' : undefined
}

/**
 * Picks the signed-in person's cases for the "Start here today" card.
 *
 * @param rows - The open cases of the person's team, before any filter or scope.
 * @param me - The signed-in person.
 * @param now - The current time.
 * @param limit - Most cases to pick; {@link START_HERE_LIMIT} by default.
 * @returns Up to `limit` of the person's own cases (those "My cases" lists, see `isMine`),
 * overdue or escalated first, then urgent, then due today; within each, the earliest due time
 * first, then the case number.
 *
 * @remarks
 * Worked out by CareLoop from data the worklist already loaded, never by AI, so the card works
 * whether AI is on, off or failing.
 */
export function startHere(
  rows: readonly WorklistRow[],
  me: StaffMember,
  now: Date,
  limit: number = START_HERE_LIMIT,
): StartHereItem[] {
  const items: StartHereItem[] = []
  for (const row of rows) {
    const reason = isMine(row, me) ? startReason(row, now) : undefined
    if (reason) {
      items.push({ row, reason })
    }
  }
  return items
    .sort(
      (a, b) =>
        START_ORDER[a.reason] - START_ORDER[b.reason] ||
        (a.row.due?.getTime() ?? Number.POSITIVE_INFINITY) - (b.row.due?.getTime() ?? Number.POSITIVE_INFINITY) ||
        a.row.summary.caseId.localeCompare(b.row.summary.caseId),
    )
    .slice(0, Math.max(0, limit))
}
