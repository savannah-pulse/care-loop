import type { CaseSummary } from '../../data'
import {
  caseStatus,
  exceptionLabel,
  isUrgent,
  programmeName,
  type CaseStatus,
  type CaseStatusKind,
  type Tone,
} from '../../domain'
import { parseInstant } from '../../format'

/**
 * The worklist's filter chips: all open cases, then one per exception queue.
 *
 * @remarks
 * The chips are the exception queues of the solution design: urgent, overdue (`Overdue`), declined
 * (`Rejected`), not reached (`Unreachable`), not acknowledged (`Unacknowledged`) and needs a new
 * owner (`Unassigned`).
 */
export const WORKLIST_FILTERS = [
  'all',
  'urgent',
  'overdue',
  'declined',
  'notReached',
  'notAcknowledged',
  'needsOwner',
] as const

/** One of the worklist's filter chips. */
export type WorklistFilter = (typeof WORKLIST_FILTERS)[number]

/** The colour of each chip's dot; the "All open" chip has none. */
export const FILTER_TONES: Readonly<Record<WorklistFilter, Tone | undefined>> = {
  all: undefined,
  urgent: 'red',
  overdue: 'red',
  declined: 'red',
  notReached: 'amber',
  notAcknowledged: 'amber',
  needsOwner: 'amber',
}

const FILTER_EXCEPTIONS: Readonly<Partial<Record<WorklistFilter, string>>> = {
  overdue: 'Overdue',
  declined: 'Rejected',
  notReached: 'Unreachable',
  notAcknowledged: 'Unacknowledged',
  needsOwner: 'Unassigned',
}

const STATUS_EXCEPTIONS: Readonly<Partial<Record<CaseStatusKind, string>>> = {
  overdue: 'Overdue',
  declined: 'Rejected',
  notReached: 'Unreachable',
  notAcknowledged: 'Unacknowledged',
  needsOwner: 'Unassigned',
}

/** One open case, prepared for the worklist table. */
export interface WorklistRow {
  /** The case as `GET /cases` returned it. */
  readonly summary: CaseSummary
  /** The status word the row shows. */
  readonly status: CaseStatus
  /** `true` when the clinician reviewed the result as urgent. */
  readonly urgent: boolean
  /** When the next action is due, when it has a due time. */
  readonly due?: Date
  /** The programme's name. */
  readonly programme: string
  /** Exceptions other than the one the status already names, in plain words. */
  readonly otherFlags: readonly string[]
}

/**
 * Prepares the open cases of a case list for the worklist.
 *
 * @param cases - The cases from `GET /cases`.
 * @returns One row per case that is not closed, in the order given.
 */
export function toWorklistRows(cases: readonly CaseSummary[]): WorklistRow[] {
  return cases
    .filter((summary) => summary.stage !== 'Closed')
    .map((summary) => {
      const status = caseStatus(summary)
      const named = STATUS_EXCEPTIONS[status.kind]
      return {
        summary,
        status,
        urgent: isUrgent(summary.urgency),
        due: parseInstant(summary.dueAt),
        programme: programmeName(summary.pathwayId),
        otherFlags: summary.exceptions.filter((name) => name !== named).map(exceptionLabel),
      }
    })
}

/**
 * Tells whether a row belongs under a filter chip.
 *
 * @param row - A worklist row.
 * @param filter - The selected chip.
 * @returns `true` when the case is in that queue; every row is in "All open".
 */
export function matchesFilter(row: WorklistRow, filter: WorklistFilter): boolean {
  if (filter === 'all') {
    return true
  }
  if (filter === 'urgent') {
    return row.urgent
  }
  const exception = FILTER_EXCEPTIONS[filter]
  return exception !== undefined && row.summary.exceptions.includes(exception)
}

/**
 * Counts the rows under each filter chip.
 *
 * @param rows - The rows the chips count, after the programme and search filters.
 * @returns The number of rows in each queue; a case can be in more than one.
 */
export function countByFilter(rows: readonly WorklistRow[]): Record<WorklistFilter, number> {
  const counts = Object.fromEntries(WORKLIST_FILTERS.map((filter) => [filter, 0])) as Record<WorklistFilter, number>
  for (const row of rows) {
    for (const filter of WORKLIST_FILTERS) {
      if (matchesFilter(row, filter)) {
        counts[filter] += 1
      }
    }
  }
  return counts
}

/**
 * Tells whether a row belongs to the chosen programme.
 *
 * @param row - A worklist row.
 * @param pathwayId - The chosen pathway, or an empty string for all programmes.
 * @returns `true` when no programme is chosen or the case follows it.
 */
export function matchesProgramme(row: WorklistRow, pathwayId: string): boolean {
  return pathwayId === '' || row.summary.pathwayId === pathwayId
}

/**
 * Tells whether a row matches what was typed in the search box.
 *
 * @param row - A worklist row.
 * @param query - The search text; blank matches everything.
 * @returns `true` when the patient reference contains the text, ignoring case.
 *
 * @remarks
 * Case reads carry only the patient reference, not a name or MRN, so search is by reference.
 */
export function matchesSearch(row: WorklistRow, query: string): boolean {
  const text = query.trim().toLowerCase()
  return text === '' || row.summary.patientReference.toLowerCase().includes(text)
}

function isPressing(row: WorklistRow): boolean {
  return row.urgent || row.status.kind === 'overdue'
}

/**
 * Orders rows by risk: urgent and overdue cases first, then by time left.
 *
 * @param rows - The rows to order.
 * @returns A new array. Within each group the earliest due time comes first, cases without a due
 * time come last, and ties go to the more pressing status, then the patient reference.
 */
export function sortByRisk(rows: readonly WorklistRow[]): WorklistRow[] {
  return [...rows].sort((a, b) => {
    const group = Number(isPressing(b)) - Number(isPressing(a))
    if (group !== 0) {
      return group
    }
    const dueA = a.due?.getTime() ?? Number.POSITIVE_INFINITY
    const dueB = b.due?.getTime() ?? Number.POSITIVE_INFINITY
    if (dueA !== dueB) {
      return dueA < dueB ? -1 : 1
    }
    if (a.status.rank !== b.status.rank) {
      return a.status.rank - b.status.rank
    }
    return a.summary.patientReference.localeCompare(b.summary.patientReference)
  })
}

/** A programme the worklist can be narrowed to. */
export interface ProgrammeOption {
  /** The pathway identifier. */
  readonly id: string
  /** The programme's name. */
  readonly name: string
}

/**
 * Lists the programmes that have open cases, for the programme filter.
 *
 * @param rows - The worklist rows.
 * @returns One option per programme, sorted by name.
 */
export function programmeOptions(rows: readonly WorklistRow[]): ProgrammeOption[] {
  const names = new Map<string, string>()
  for (const row of rows) {
    names.set(row.summary.pathwayId, row.programme)
  }
  return [...names].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name))
}

/**
 * Finds the earliest due time among rows.
 *
 * @param rows - The rows to look through.
 * @returns The next deadline, or `undefined` when no row has a due time.
 */
export function nextDeadline(rows: readonly WorklistRow[]): Date | undefined {
  let earliest: Date | undefined
  for (const row of rows) {
    if (row.due && (!earliest || row.due < earliest)) {
      earliest = row.due
    }
  }
  return earliest
}
