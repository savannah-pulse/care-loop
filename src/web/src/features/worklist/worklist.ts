import type { CaseSummary } from '../../data'
import {
  caseStatus,
  describePatient,
  displayLabelText,
  isUrgent,
  nextActionReason,
  programmeName,
  type CaseStatus,
  type PatientView,
  type Tone,
} from '../../domain'
import { isSameDay, parseInstant } from '../../format'

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

/**
 * The groups the worklist puts open cases in, most pressing first.
 *
 * @remarks
 * - `now`, "Needs action now": overdue, reviewed as urgent, or escalated.
 * - `today`, "Due today": due later today in Abu Dhabi.
 * - `later`, "Coming up": everything else, including cases without a due time.
 */
export const WORKLIST_GROUPS = ['now', 'today', 'later'] as const

/** One of the worklist's groups. */
export type WorklistGroup = (typeof WORKLIST_GROUPS)[number]

/** One open case, prepared for the worklist. */
export interface WorklistRow {
  /** The case as `GET /cases` returned it. */
  readonly summary: CaseSummary
  /** The status word the case shows. */
  readonly status: CaseStatus
  /** `true` when the clinician reviewed the result as urgent. */
  readonly urgent: boolean
  /** When the next action is due, when it has a due time. */
  readonly due?: Date
  /** The patient: their name and MRN, a friendly number, or a case number when names are hidden. */
  readonly patient: PatientView
  /** The programme's name. */
  readonly programme: string
  /** The display stage the API reports, in plain words, for example `Follow-up`. */
  readonly stage: string
  /** Why the next action is needed, in a few words, when there is something to explain. */
  readonly reason?: string
}

/**
 * Prepares the open cases of a case list for the worklist.
 *
 * @param cases - The cases from `GET /cases`.
 * @param viewerRole - The role of the person reading, which decides whether names are hidden.
 * @returns One row per case that is not closed, in the order given.
 */
export function toWorklistRows(cases: readonly CaseSummary[], viewerRole: string): WorklistRow[] {
  return cases
    .filter((summary) => summary.stage !== 'Closed')
    .map((summary) => ({
      summary,
      status: caseStatus(summary),
      urgent: isUrgent(summary.urgency),
      due: parseInstant(summary.dueAt),
      patient: describePatient(summary, viewerRole),
      programme: programmeName(summary.pathwayId),
      stage: displayLabelText(summary.displayLabel),
      reason: nextActionReason(summary),
    }))
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

function compact(text: string): string {
  return text.toLowerCase().replace(/\s+/g, '')
}

/**
 * Tells whether a row matches what was typed in the search box.
 *
 * @param row - A worklist row.
 * @param query - The search text; blank matches everything.
 * @returns `true` when the patient's name, MRN, reference or, where names are hidden, case number
 * contains the text, ignoring case and spaces, so `hessa`, `100 245` and `100245` all find Hessa
 * Al Dhaheri, MRN 100 245 891.
 */
export function matchesSearch(row: WorklistRow, query: string): boolean {
  const text = compact(query)
  return text === '' || compact(row.patient.searchText).includes(text)
}

/**
 * Tells whether a case's next action is late.
 *
 * @param row - A worklist row.
 * @param now - The current time.
 * @returns `true` when its due time has passed or the API reports it as overdue.
 */
export function isOverdue(row: WorklistRow, now: Date): boolean {
  return row.status.kind === 'overdue' || (row.due !== undefined && row.due.getTime() < now.getTime())
}

/**
 * Works out which group a case goes in.
 *
 * @param row - A worklist row.
 * @param now - The current time.
 * @returns `now` for overdue, urgent or escalated cases; `today` for cases due later today in Abu
 * Dhabi; `later` for everything else.
 */
export function groupOf(row: WorklistRow, now: Date): WorklistGroup {
  if (isOverdue(row, now) || row.urgent || row.summary.isEscalated) {
    return 'now'
  }
  return row.due && isSameDay(row.due, now) ? 'today' : 'later'
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
    return (a.summary.patientReference ?? a.summary.caseId).localeCompare(b.summary.patientReference ?? b.summary.caseId)
  })
}

/** One group of the worklist and its cases. */
export interface WorklistSection {
  /** Which group this is. */
  readonly group: WorklistGroup
  /** Its cases, most at risk first. */
  readonly rows: readonly WorklistRow[]
}

/**
 * Puts rows into the worklist's groups.
 *
 * @param rows - The rows to group, after filtering.
 * @param now - The current time.
 * @returns The groups that have cases, in the order of {@link WORKLIST_GROUPS}, each ordered by
 * {@link sortByRisk}. Empty groups are left out.
 */
export function groupRows(rows: readonly WorklistRow[], now: Date): WorklistSection[] {
  return WORKLIST_GROUPS.map((group) => ({
    group,
    rows: sortByRisk(rows.filter((row) => groupOf(row, now) === group)),
  })).filter((section) => section.rows.length > 0)
}

/** The numbers in the worklist's summary line. */
export interface WorklistCounts {
  /** Open cases. */
  readonly open: number
  /** Open cases whose next action is late. */
  readonly overdue: number
  /** Open cases due later today in Abu Dhabi. */
  readonly dueToday: number
}

/**
 * Counts the open cases for the worklist's summary line.
 *
 * @param rows - Every open case, before any filter.
 * @param now - The current time.
 * @returns How many cases are open, overdue and due later today.
 */
export function countOpen(rows: readonly WorklistRow[], now: Date): WorklistCounts {
  const overdue = rows.filter((row) => isOverdue(row, now)).length
  const dueToday = rows.filter((row) => !isOverdue(row, now) && row.due && isSameDay(row.due, now)).length
  return { open: rows.length, overdue, dueToday }
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
