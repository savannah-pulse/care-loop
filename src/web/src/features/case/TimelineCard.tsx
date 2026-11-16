import { useEffect, useId, useRef, useState } from 'react'
import { Badge, ReadView } from '../../components'
import type { Read, TimelineEntry } from '../../data'
import {
  describeActor,
  groupByDay,
  hasOwnReason,
  newestFirst,
  sourceLabel,
  timelineEntryTitle,
  timelineKind,
  timelineSource,
} from '../../domain'
import { formatDayHeading, formatTime, parseInstant } from '../../format'
import { t, tp } from '../../i18n'

/** How many timeline entries show before "Show earlier events". */
export const TIMELINE_PAGE = 7

/** Props for {@link TimelineCard}. */
export interface TimelineCardProps {
  /** The read of the case's timeline, from `useCaseTimeline`. */
  readonly read: Read<TimelineEntry[]>
}

interface ItemProps {
  readonly entry: TimelineEntry
  readonly focusTarget?: boolean
}

function TimelineItem({ entry, focusTarget = false }: ItemProps) {
  const ref = useRef<HTMLLIElement>(null)
  const at = parseInstant(entry.sourceTimestamp)
  const actor = describeActor(entry.actorReference, entry.actorRole)
  const refusedReason =
    entry.disposition === 'Rejected' && entry.eventType !== 'EscalationAcknowledged' && hasOwnReason(entry)
      ? entry.reason
      : undefined
  useEffect(() => {
    if (focusTarget) {
      ref.current?.focus()
    }
  }, [focusTarget])
  return (
    <li className="tl-item" ref={ref} tabIndex={focusTarget ? -1 : undefined}>
      <time className="tl-time" dateTime={entry.sourceTimestamp}>
        {at ? formatTime(at) : entry.sourceTimestamp}
      </time>
      <div className="tl-body">
        <span className="tl-kind">
          {timelineKind(entry.eventType)}
          {entry.disposition === 'Rejected' ? <Badge tone="red">{t('timeline.refused')}</Badge> : null}
          <span className="tl-source" title={entry.sourceSystem}>
            {sourceLabel(timelineSource(entry))}
          </span>
        </span>
        <p>{timelineEntryTitle(entry)}</p>
        {refusedReason ? <p className="tl-by">{refusedReason}</p> : null}
        {actor ? (
          <p className="tl-by" title={entry.actorReference}>
            {actor}
          </p>
        ) : null}
      </div>
    </li>
  )
}

function TimelineList({ entries }: { readonly entries: readonly TimelineEntry[] }) {
  const [expanded, setExpanded] = useState(false)
  const ordered = newestFirst(entries)
  if (ordered.length === 0) {
    return <p className="note">{t('case.timelineEmpty')}</p>
  }
  const shown = expanded ? ordered : ordered.slice(0, TIMELINE_PAGE)
  const hidden = ordered.length - shown.length
  const firstRevealed = expanded ? ordered[TIMELINE_PAGE]?.auditEventId : undefined
  return (
    <>
      <ol className="tl-days">
        {groupByDay(shown).map((day) => (
          <li key={day.key}>
            <h3 className="tl-day-title">
              {day.date ? formatDayHeading(day.date) : day.entries[0]?.sourceTimestamp}
            </h3>
            <ol className="tl">
              {day.entries.map((entry) => (
                <TimelineItem
                  key={entry.auditEventId}
                  entry={entry}
                  focusTarget={entry.auditEventId === firstRevealed}
                />
              ))}
            </ol>
          </li>
        ))}
      </ol>
      {hidden > 0 ? (
        <button type="button" className="linkbtn linkrow" onClick={() => setExpanded(true)}>
          {tp('case.showEarlier', hidden)}
        </button>
      ) : null}
    </>
  )
}

/**
 * The case's timeline, newest first and grouped by day: what happened, what kind of step it was,
 * and who did it where.
 *
 * @remarks
 * Reads `GET /cases/{caseId}/timeline`, the case's audit trail. Each entry names its source system
 * in plain words (CareLoop, Hospital system, or Text message or email, C3) and who acted, when a person
 * did. Its main line is its reason when the reason says more than the kind of event, and otherwise
 * the kind in words; nothing is made up. Events CareLoop refused, such as a closure without
 * evidence or an acknowledgement by the wrong role, are marked "Refused" and never read as done.
 * The first {@link TIMELINE_PAGE} entries show, with a button for the earlier ones; once they show,
 * focus moves to the first of them.
 */
export function TimelineCard({ read }: TimelineCardProps) {
  const headingId = useId()
  return (
    <section className="card timeline" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {t('case.timelineTitle')}
        <span className="r small muted">{t('case.timelineOrder')}</span>
      </h2>
      <ReadView
        read={read}
        loadingLabel={t('case.timelineLoading')}
        notAvailable={t('case.timelineNotAvailable')}
        errorTitle={t('case.timelineError')}
      >
        {(entries) => <TimelineList entries={entries} />}
      </ReadView>
    </section>
  )
}
