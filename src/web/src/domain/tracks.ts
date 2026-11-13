import { hasMessage, t } from '../i18n'
import { formatDateTime, parseInstant } from '../format'
import type { TrackKind, TrackStatus } from '../data'
import type { Tone } from './tone'

/** The four parallel tracks, in the order the case screen lists them. */
export const TRACK_KINDS: readonly TrackKind[] = ['Notification', 'Consultation', 'Referral', 'Appointment']

const RED_STATUSES: readonly string[] = ['Rejected', 'Missed']
const AMBER_STATUSES: readonly string[] = ['Failed']

/** One track as the case screen shows it. */
export interface TrackView {
  /** Which track this is. */
  readonly kind: TrackKind
  /** The track's name, for example `Patient contact`. */
  readonly name: string
  /** The track's status in plain words, for example `Declined` or `Not started`. */
  readonly status: string
  /** The tone of the status badge. */
  readonly tone: Tone
  /** When the track last changed, in words; absent when it has not started. */
  readonly updated?: string
  /** A reminder of what the status does not mean, for example that delivery is not acknowledgement. */
  readonly note?: string
}

function statusTone(track: TrackStatus): Tone {
  const status = track.status ?? ''
  if (track.isComplete) {
    return 'green'
  }
  if (RED_STATUSES.includes(status)) {
    return 'red'
  }
  return AMBER_STATUSES.includes(status) ? 'amber' : 'blue'
}

function statusNote(kind: TrackKind, status: string): string | undefined {
  if (kind === 'Notification' && status === 'Delivered') {
    return t('tracks.notes.delivered')
  }
  if (kind === 'Referral' && status === 'Accepted') {
    return t('tracks.notes.referralAccepted')
  }
  return undefined
}

/**
 * Describes one track of a case for the case screen.
 *
 * @param kind - Which track to describe.
 * @param track - Its state from `GET /cases/{caseId}`, or `undefined` when the API did not list it.
 * @returns The track's name, status, badge tone, last change and any reminder.
 *
 * @remarks
 * A track that has reached its completing status is green. Delivery alone does not complete the
 * notification track, and an accepted referral is still not completed care, so both carry a note.
 */
export function describeTrack(kind: TrackKind, track: TrackStatus | undefined): TrackView {
  const name = t(`tracks.kinds.${kind}`)
  const status = track?.status
  if (!track || !status) {
    return { kind, name, status: t('tracks.notStarted'), tone: 'grey' }
  }
  const key = `tracks.statuses.${kind}.${status}`
  const updatedAt = parseInstant(track.updatedAt)
  return {
    kind,
    name,
    status: hasMessage(key) ? t(key) : status,
    tone: statusTone(track),
    updated: updatedAt ? t('tracks.updated', { date: formatDateTime(updatedAt) }) : undefined,
    note: statusNote(kind, status),
  }
}
