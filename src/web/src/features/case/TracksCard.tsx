import { useId } from 'react'
import { Badge, Icon, type IconName } from '../../components'
import type { TrackKind, TrackStatus } from '../../data'
import { TRACK_KINDS, describeTrack } from '../../domain'
import { t } from '../../i18n'

/** Props for {@link TracksCard}. */
export interface TracksCardProps {
  /** The case's tracks, from `GET /cases/{caseId}`. */
  readonly tracks: readonly TrackStatus[]
}

const TRACK_ICONS: Readonly<Record<TrackKind, IconName>> = {
  Notification: 'phone',
  Consultation: 'chat',
  Referral: 'send',
  Appointment: 'calendar',
}

/**
 * "How each part is going": the four parallel tracks of a case, each with an icon and its status in
 * words.
 *
 * @remarks
 * Patient contact, consultation, referral and appointment run side by side, so the stage bar shows
 * only the furthest step and this card shows each one. A track that has not started says so.
 */
export function TracksCard({ tracks }: TracksCardProps) {
  const headingId = useId()
  return (
    <section className="card tracks-card" aria-labelledby={headingId}>
      <h2 className="card-title" id={headingId}>
        {t('case.tracksTitle')}
      </h2>
      <ul className="tracks">
        {TRACK_KINDS.map((kind) => {
          const view = describeTrack(
            kind,
            tracks.find((track) => track.kind === kind),
          )
          return (
            <li key={kind}>
              <span className="track-icon" aria-hidden="true">
                <Icon name={TRACK_ICONS[kind]} />
              </span>
              <div className="track-text">
                <span className="track-head">
                  <b>{view.name}</b>
                  <Badge tone={view.tone}>{view.status}</Badge>
                </span>
                {view.updated ? <span className="sub-line">{view.updated}</span> : null}
                {view.note ? <span className="sub-line">{view.note}</span> : null}
              </div>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
