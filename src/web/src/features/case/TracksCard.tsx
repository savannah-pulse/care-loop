import { useId } from 'react'
import { Badge } from '../../components'
import type { TrackStatus } from '../../data'
import { TRACK_KINDS, describeTrack } from '../../domain'
import { t } from '../../i18n'

/** Props for {@link TracksCard}. */
export interface TracksCardProps {
  /** The case's tracks, from `GET /cases/{caseId}`. */
  readonly tracks: readonly TrackStatus[]
}

/**
 * "Where each part stands": the four parallel tracks of a case, each with its own status.
 *
 * @remarks
 * Patient contact, consultation, referral and appointment run side by side, so the stage bar shows
 * only the furthest step and this card shows each one. A track that has not started says so.
 */
export function TracksCard({ tracks }: TracksCardProps) {
  const headingId = useId()
  return (
    <section className="card" aria-labelledby={headingId}>
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
              <div>
                <b>{view.name}</b>
                {view.updated ? <span className="sub-line">{view.updated}</span> : null}
                {view.note ? <span className="sub-line">{view.note}</span> : null}
              </div>
              <Badge tone={view.tone}>{view.status}</Badge>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
