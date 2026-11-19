import type { CaseDetail } from '../data'
import { parseInstant } from '../format'

/**
 * A change the signed-in person made to a case on a screen, such as a recorded call or an
 * acknowledged escalation.
 */
export interface CaseChange {
  /** The case as the screen showed it when the person made the change. */
  readonly base: CaseDetail
  /** The case as the API returned it after the change. */
  readonly detail: CaseDetail
}

/**
 * Chooses which version of a case a screen shows once the signed-in person has changed it.
 *
 * @param read - The case from the screen's latest read.
 * @param change - The person's latest change to the case on this screen, if any.
 * @returns The case as the API returned it after the change, while the latest read is the one the
 * change was made on or an older one; otherwise the latest read.
 *
 * @remarks
 * Screens read their case again by themselves. A read already on its way when the person made the
 * change can arrive after the API's answer to it, still holding the case as it was before, and
 * showing it would undo the change on screen until the next read. The API moves a case's
 * `updatedAt` forward with every change, so such a read is recognisably older; a read as new as the
 * change, or newer, replaces it.
 */
export function latestCase(read: CaseDetail, change: CaseChange | undefined): CaseDetail {
  if (change === undefined) {
    return read
  }
  if (read === change.base) {
    return change.detail
  }
  const readAt = parseInstant(read.updatedAt)
  const changedAt = parseInstant(change.detail.updatedAt)
  return readAt !== undefined && changedAt !== undefined && readAt.getTime() < changedAt.getTime()
    ? change.detail
    : read
}
