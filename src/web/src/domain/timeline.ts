import { t } from '../i18n'
import { dayKey, parseInstant } from '../format'
import { namesHiddenFor } from '../identity'
import type { CaseEventType, TimelineEntry } from '../data'

/**
 * Names a timeline entry in plain words, for an entry whose reason says nothing more.
 *
 * @param eventType - The kind of event, from the API.
 * @returns For example `Referral updated` or `Handoff summary approved`.
 *
 * @remarks
 * When an entry's reason says what happened, it is the entry's main line; this title stands in when
 * the reason is empty or only repeats the kind (see {@link hasOwnReason}).
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

/** Where a timeline entry came from: CareLoop itself, a hospital system or the patient channel. */
export type TimelineSource = 'careLoop' | 'hospital' | 'patientChannel'

/**
 * Source systems the dashboard names, by the key the API reports in `sourceSystem`.
 *
 * @remarks
 * CareLoop records its own events (deadlines, escalations, acknowledgements and approved summaries)
 * as `careloop`. Every other system that reaches CareLoop is a hospital system, through its FHIR
 * boundary, unless it is listed here as the patient channel.
 */
export const SOURCE_SYSTEMS: Readonly<Record<string, TimelineSource>> = {
  careloop: 'careLoop',
  'patient-channel': 'patientChannel',
}

/**
 * Works out where a timeline entry came from (C3).
 *
 * @param entry - The entry, with the source system the API joined from the stored event.
 * @returns `careLoop` for CareLoop's own events, `patientChannel` for the patient channel and
 * `hospital` for any other system.
 */
export function timelineSource(entry: Pick<TimelineEntry, 'sourceSystem'>): TimelineSource {
  return SOURCE_SYSTEMS[entry.sourceSystem.trim().toLowerCase()] ?? 'hospital'
}

/**
 * Names where a timeline entry came from, in plain words.
 *
 * @param source - Where the entry came from.
 * @returns `CareLoop`, `Hospital system` or `Patient channel`.
 */
export function sourceLabel(source: TimelineSource): string {
  return t(`timeline.sources.${source}`)
}

/**
 * Reasons the API writes on every entry of a kind, which say nothing more than the kind itself.
 *
 * @remarks
 * Lower case. The API records "Overdue deadline checked; exception flags or escalation steps
 * raised" on every overdue `DeadlineElapsed` entry; the screen names the kind in words instead.
 */
export const GENERIC_REASONS: readonly string[] = ['overdue deadline checked; exception flags or escalation steps raised']

/**
 * Tells whether an entry's reason says more than its kind.
 *
 * @param entry - The entry's kind and reason.
 * @returns `false` when the reason is empty, only repeats the kind and whether it was applied or
 * refused, as in `ReferralUpdated applied`, or is one of the {@link GENERIC_REASONS}; the screen
 * then names the kind in words instead.
 */
export function hasOwnReason(entry: Pick<TimelineEntry, 'eventType' | 'reason'>): boolean {
  const reason = entry.reason.trim().toLowerCase()
  const kind = entry.eventType.toLowerCase()
  return reason !== '' && reason !== `${kind} applied` && reason !== `${kind} rejected` && !GENERIC_REASONS.includes(reason)
}

/**
 * Tells whether the API's own wording of a timeline entry may be shown to a role.
 *
 * @param entry - The entry's source system.
 * @param viewerRole - The role of the person reading.
 * @returns `true`, except for an entry from a hospital system or the patient channel read by a role
 * names are hidden from.
 *
 * @remarks
 * The wording of such an entry can quote the hospital system's own numbers, as in "A case already
 * exists for result 'fhc-his/DiagnosticReport/r-…'", or free text from the hospital system, and
 * either can point to the patient. CareLoop's own wording, such as "Deadline warning raised", never
 * does, so it is always shown.
 */
export function mayQuoteReason(entry: Pick<TimelineEntry, 'sourceSystem'>, viewerRole: string): boolean {
  return !namesHiddenFor(viewerRole) || timelineSource(entry) === 'careLoop'
}

/**
 * Words the main line of a timeline entry.
 *
 * @param entry - The entry's kind, disposition, reason and source system.
 * @param viewerRole - The role of the person reading.
 * @returns For an entry CareLoop refused, what was refused in plain words, so a refused attempt
 * never reads as done: for example `Acknowledgement refused` or `Referral update not accepted`.
 * Otherwise the reason when it says more than the kind (see {@link hasOwnReason}) and may be shown
 * to the role (see {@link mayQuoteReason}), or the kind in words.
 *
 * @remarks
 * The API writes refused acknowledgements to the timeline as `EscalationAcknowledged` entries with
 * the disposition `Rejected`, even the programme coordinator's, so the disposition decides the
 * words, never the kind alone.
 */
export function timelineEntryTitle(
  entry: Pick<TimelineEntry, 'eventType' | 'disposition' | 'reason' | 'sourceSystem'>,
  viewerRole: string,
): string {
  if (entry.disposition === 'Rejected') {
    return t(`timeline.refusedEvents.${entry.eventType}`)
  }
  return hasOwnReason(entry) && mayQuoteReason(entry, viewerRole) ? entry.reason : timelineTitle(entry.eventType)
}

/**
 * Says why CareLoop refused a timeline entry, for a line under it.
 *
 * @param entry - The entry's kind, disposition, reason and source system.
 * @param viewerRole - The role of the person reading.
 * @returns The reason CareLoop recorded, when the entry was refused, the reason says more than the
 * kind (see {@link hasOwnReason}) and it may be shown to the role (see {@link mayQuoteReason});
 * otherwise `undefined`.
 *
 * @remarks
 * A refused acknowledgement never shows its reason: the API writes it for the audit trail, with
 * raw references and timestamps, and the main line already says it was refused.
 */
export function refusalReason(
  entry: Pick<TimelineEntry, 'eventType' | 'disposition' | 'reason' | 'sourceSystem'>,
  viewerRole: string,
): string | undefined {
  const shown =
    entry.disposition === 'Rejected' &&
    entry.eventType !== 'EscalationAcknowledged' &&
    hasOwnReason(entry) &&
    mayQuoteReason(entry, viewerRole)
  return shown ? entry.reason : undefined
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
