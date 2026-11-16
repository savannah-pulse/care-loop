import type { CaseRow } from './CaseTable'

/**
 * Orders attention rows ahead of unflagged rows, then by the source deadline.
 * @param rows - Authorized coordination rows.
 * @returns A new sorted array; urgency is never interpreted or changed.
 * @remarks A row with no deadline sorts after dated rows of the same kind, then by case id, so a missing
 * deadline never reorders the rows that do have one.
 */
export function sortCases<T extends CaseRow>(rows: readonly T[]): T[] {
  const due = (row: CaseRow) => row.dueAt == null ? Number.POSITIVE_INFINITY : Date.parse(row.dueAt)
  return [...rows].sort((a, b) => Number(b.exceptions.includes('Overdue')) - Number(a.exceptions.includes('Overdue'))
    || Number(b.exceptions.length > 0) - Number(a.exceptions.length > 0)
    || (due(a) === due(b) ? 0 : due(a) < due(b) ? -1 : 1)
    || a.caseId.localeCompare(b.caseId))
}
