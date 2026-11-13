import { t } from '../i18n'
import { parseInstant } from '../format'
import type { CaseEventType, TimelineEntry } from '../data'

/**
 * Names a timeline entry in plain words.
 *
 * @param eventType - The kind of event, from the API.
 * @returns For example `Referral updated` or `Handoff summary approved`.
 *
 * @remarks
 * The API reports the kind of event and a reason, not which track status it set, so the title is
 * the event and the reason underneath says what happened.
 */
export function timelineTitle(eventType: CaseEventType): string {
  return t(`timeline.events.${eventType}`)
}

function instant(entry: TimelineEntry): number {
  return (parseInstant(entry.sourceTimestamp) ?? parseInstant(entry.receivedTimestamp))?.getTime() ?? 0
}

/**
 * Orders timeline entries newest first.
 *
 * @param entries - Entries in any order.
 * @returns A new array, newest first by when the action happened, then by when CareLoop received it.
 */
export function newestFirst(entries: readonly TimelineEntry[]): TimelineEntry[] {
  return [...entries].sort((a, b) => {
    const byAction = instant(b) - instant(a)
    if (byAction !== 0) {
      return byAction
    }
    return (
      (parseInstant(b.receivedTimestamp)?.getTime() ?? 0) - (parseInstant(a.receivedTimestamp)?.getTime() ?? 0)
    )
  })
}
