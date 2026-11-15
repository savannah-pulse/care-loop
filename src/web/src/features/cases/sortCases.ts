import type { CaseRow } from './CaseTable'

/**
 * Orders attention rows ahead of unflagged rows, then by the source deadline.
 * @param rows - Authorized coordination rows.
 * @returns A new sorted array; urgency is never interpreted or changed.
 */
export function sortCases<T extends CaseRow>(rows: readonly T[]): T[] {
  return [...rows].sort((a, b) => Number(b.exceptions.includes('Overdue')) - Number(a.exceptions.includes('Overdue'))
    || Number(b.exceptions.length > 0) - Number(a.exceptions.length > 0)
    || (a.dueAt == null ? Infinity : Date.parse(a.dueAt)) - (b.dueAt == null ? Infinity : Date.parse(b.dueAt))
    || a.caseId.localeCompare(b.caseId))
}
