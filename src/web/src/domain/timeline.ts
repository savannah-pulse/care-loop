import { t } from '../i18n'
import { dayKey, parseInstant } from '../format'
import type { CaseEventType, TimelineEntry } from '../data'

/**
 * Names a timeline entry in plain words, for an entry that has no reason to show.
 *
 * @param eventType - The kind of event, from the API.
 * @returns For example `Referral updated` or `Handoff summary approved`.
 *
 * @remarks
 * Each entry's reason says what happened, so it is the entry's main line; this title stands in
 * only when the reason is empty.
 */
export function timelineTitle(eventType: CaseEventType): string {
  return t(`timeline.events.${eventType}`)
}

/**
 * Names the kind of a timeline entry in a word or two, for the small label next to it.
 *
 * @param eventType - The kind of event, from the API.
 * @returns For example `Referral`, `Patient contact`, `Deadline` or `Summary`.
 */
export function timelineKind(eventType: CaseEventType): string {
  return t(`timeline.kinds.${eventType}`)
}

/** Where a timeline entry was recorded, as far as the dashboard can tell. */
export type TimelineSource = 'careLoop' | 'hospital'

/**
 * Works out where a timeline entry was recorded.
 *
 * @param entry - The entry's kind and the role its actor acted in.
 * @returns `undefined` for CareLoop's own actions, whose actor already reads `CareLoop`;
 * `careLoop` for a handoff summary decision, which people make in this dashboard; and `hospital`
 * for anything else a person did.
 *
 * @remarks
 * Timeline entries carry no source system yet (C3). People record clinical steps in the hospital
 * system, which reaches CareLoop only through its FHIR boundary; the one thing a person does in
 * CareLoop itself is decide on a handoff summary.
 */
export function timelineSource(entry: Pick<TimelineEntry, 'eventType' | 'actorRole'>): TimelineSource | undefined {
  if (entry.actorRole === 'system') {
    return undefined
  }
  return entry.eventType === 'HandoffSummaryApproved' ? 'careLoop' : 'hospital'
}

function instant(entry: TimelineEntry): Date | undefined {
  return parseInstant(entry.sourceTimestamp) ?? parseInstant(entry.receivedTimestamp)
}

/**
 * Orders timeline entries newest first.
 *
 * @param entries - Entries in any order.
 * @returns A new array, newest first by when the action happened, then by when CareLoop received it.
 */
export function newestFirst(entries: readonly TimelineEntry[]): TimelineEntry[] {
  return [...entries].sort((a, b) => {
    const byAction = (instant(b)?.getTime() ?? 0) - (instant(a)?.getTime() ?? 0)
    if (byAction !== 0) {
      return byAction
    }
    return (
      (parseInstant(b.receivedTimestamp)?.getTime() ?? 0) - (parseInstant(a.receivedTimestamp)?.getTime() ?? 0)
    )
  })
}

/** The entries of a timeline that happened on one day. */
export interface TimelineDay {
  /** The day in Abu Dhabi as `YYYY-MM-DD`, or an empty string when the entries' time cannot be read. */
  readonly key: string
  /** An instant on that day, to write its heading; absent when the time cannot be read. */
  readonly date?: Date
  /** The day's entries, in the order given. */
  readonly entries: readonly TimelineEntry[]
}

/**
 * Groups timeline entries by the day they happened on, in Abu Dhabi time.
 *
 * @param entries - Entries in the order to show them, usually from {@link newestFirst}.
 * @returns One group per day, in the order the days first appear, each keeping the entries' order.
 */
export function groupByDay(entries: readonly TimelineEntry[]): TimelineDay[] {
  const days: { key: string; date?: Date; entries: TimelineEntry[] }[] = []
  for (const entry of entries) {
    const date = instant(entry)
    const key = date ? dayKey(date) : ''
    const day = days.find((candidate) => candidate.key === key)
    if (day) {
      day.entries.push(entry)
    } else {
      days.push({ key, date, entries: [entry] })
    }
  }
  return days
}
