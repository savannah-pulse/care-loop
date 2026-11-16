import { useId } from 'react'
import { Badge } from '../../components'
import type { Escalation, TimelineEntry } from '../../data'
import { escalationNotices, escalationTarget, noticeActor, sourceLabel, type Notice } from '../../domain'
import { formatDateTime } from '../../format'
import { useSignedInStaff } from '../../identity'
import { t } from '../../i18n'

/** Props for {@link NoticeHistory}. */
export interface NoticeHistoryProps {
  /** Every escalation on the case. */
  readonly escalations: readonly Escalation[]
  /** The case's timeline, for reminders and refused acknowledgements; absent while it loads. */
  readonly timeline?: readonly TimelineEntry[]
}

function noticeTitle(notice: Notice, meReference: string): string {
  switch (notice.kind) {
    case 'reminder':
      return t('escalation.notices.reminder')
    case 'raised':
      return t('escalation.notices.raised', {
        target: notice.target ? escalationTarget(notice.target).inSentence : t('escalation.notices.nextLevel'),
      })
    case 'acknowledged':
      return t('escalation.notices.acknowledged', {
        name: notice.actor ? noticeActor(notice.actor, meReference) : t('escalation.notices.someone'),
      })
    case 'refused':
      return t('escalation.notices.refused')
    case 'resolved':
      return t('escalation.notices.resolved')
  }
}

function noticeDetail(notice: Notice, meReference: string): string | undefined {
  switch (notice.kind) {
    case 'reminder':
      return notice.reason
    case 'raised':
      return notice.level ? t('escalation.notices.level', { level: notice.level }) : undefined
    case 'acknowledged':
      return t('escalation.notices.acknowledgedDetail')
    case 'refused':
      return notice.actor ? t('escalation.notices.triedBy', { name: noticeActor(notice.actor, meReference) }) : undefined
    case 'resolved':
      return t('escalation.notices.resolvedDetail')
  }
}

/**
 * "What happened": the notice history of a case's escalations, newest first (board 17).
 *
 * @remarks
 * Built from the case's escalations (raised, acknowledged, resolved) and its timeline: reminders
 * (`DeadlineElapsed` entries that raised no escalation) and acknowledgements CareLoop refused. A
 * refused acknowledgement says so; it is never shown as an acknowledgement. Every notice comes from
 * CareLoop itself.
 */
export function NoticeHistory({ escalations, timeline }: NoticeHistoryProps) {
  const me = useSignedInStaff()
  const headingId = useId()
  const notices = escalationNotices(escalations, timeline ?? [])
  return (
    <section className="card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {t('escalation.historyTitle')}
        <span className="r small muted">{t('case.timelineOrder')}</span>
      </h2>
      {notices.length === 0 ? (
        <p className="note">{t('escalation.historyEmpty')}</p>
      ) : (
        <ol className="tl notices">
          {notices.map((notice) => {
            const detail = noticeDetail(notice, me.reference)
            return (
              <li key={notice.key} className="tl-item notice-item">
                <time className="tl-time wide" dateTime={notice.at.toISOString()}>
                  {formatDateTime(notice.at)}
                </time>
                <div className="tl-body">
                  <span className="tl-kind">
                    {notice.kind === 'reminder' ? t('timeline.kinds.DeadlineElapsed') : t('escalation.label')}
                    {notice.kind === 'refused' ? <Badge tone="red">{t('timeline.refused')}</Badge> : null}
                    <span className="tl-source">{sourceLabel('careLoop')}</span>
                  </span>
                  <p>{noticeTitle(notice, me.reference)}</p>
                  {detail ? <p className="tl-by">{detail}</p> : null}
                </div>
              </li>
            )
          })}
        </ol>
      )}
    </section>
  )
}
