import { useId, useState } from 'react'
import { Badge, ReadView } from '../../components'
import type { Read, TimelineEntry } from '../../data'
import { describeActor, newestFirst, timelineTitle } from '../../domain'
import { formatDateTime, parseInstant } from '../../format'
import { t, tp } from '../../i18n'

/** How many timeline entries show before "Show earlier events". */
export const TIMELINE_PAGE = 7

/** Props for {@link TimelineCard}. */
export interface TimelineCardProps {
  /** The read of the case's timeline, from `useCaseTimeline`. */
  readonly read: Read<TimelineEntry[]>
}

function TimelineItem({ entry }: { readonly entry: TimelineEntry }) {
  const at = parseInstant(entry.sourceTimestamp)
  return (
    <li>
      <time dateTime={entry.sourceTimestamp}>{at ? formatDateTime(at) : entry.sourceTimestamp}</time>
      <div>
        <b>
          {timelineTitle(entry.eventType)}
          {entry.disposition === 'Rejected' ? <Badge tone="red">{t('timeline.refused')}</Badge> : null}
        </b>
        {entry.reason ? <span className="by">{entry.reason}</span> : null}
      </div>
      <span className="src">{describeActor(entry.actorReference, entry.actorRole)}</span>
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
  return (
    <>
      <ul className="tl">
        {shown.map((entry) => (
          <TimelineItem key={entry.auditEventId} entry={entry} />
        ))}
      </ul>
      {hidden > 0 ? (
        <button type="button" className="linkbtn linkrow" onClick={() => setExpanded(true)}>
          {tp('case.showEarlier', hidden)}
        </button>
      ) : null}
    </>
  )
}

/**
 * The case's timeline, newest first: what happened, why, and who or what did it.
 *
 * @remarks
 * Reads `GET /cases/{caseId}/timeline`, the case's audit trail. Events CareLoop refused, such as a
 * closure without evidence, are marked "Refused". The first {@link TIMELINE_PAGE} entries show,
 * with a button for the earlier ones.
 */
export function TimelineCard({ read }: TimelineCardProps) {
  const headingId = useId()
  return (
    <section className="card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {t('case.timelineTitle')}
        <span className="r small muted">{t('case.timelineOrder')}</span>
      </h2>
      <ReadView
        read={read}
        loading={t('case.timelineLoading')}
        notAvailable={t('case.timelineNotAvailable')}
        errorTitle={t('case.timelineError')}
      >
        {(entries) => <TimelineList entries={entries} />}
      </ReadView>
    </section>
  )
}
