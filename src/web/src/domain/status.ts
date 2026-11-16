import { t } from '../i18n'
import type { CaseStage } from '../data'
import { isUrgent } from './urgency'
import type { Tone } from './tone'

/**
 * The one status word a case shows on the worklist and the case screen.
 *
 * @remarks
 * Listed from most to least pressing; a case shows the first that applies. Each comes from what
 * the API reports: the stage, the clinician-reviewed urgency, the unresolved exceptions and the
 * next action.
 */
export const CASE_STATUS_KINDS = [
  'closed',
  'overdue',
  'urgent',
  'declined',
  'notReached',
  'notAcknowledged',
  'needsOwner',
  'callBack',
  'waitingAcceptance',
  'onTrack',
] as const

/** One of the statuses a case can show. */
export type CaseStatusKind = (typeof CASE_STATUS_KINDS)[number]

/** A case's status as shown on screen: a word with a coloured dot. */
export interface CaseStatus {
  /** Which status applies. */
  readonly kind: CaseStatusKind
  /** The colour of the dot. Never used without the word. */
  readonly tone: Tone
  /** The status word, for example `Overdue`. */
  readonly label: string
  /** How pressing the status is: 0 is the most pressing. */
  readonly rank: number
}

/** The parts of a case the status is worked out from; both list rows and full cases have them. */
export interface CaseStatusInput {
  /** The case's stage in the API. */
  readonly stage: CaseStage
  /** The clinician-reviewed urgency code. */
  readonly urgency?: string | null
  /** Names of the unresolved exceptions. */
  readonly exceptions: readonly string[]
  /** Key of the single next action. */
  readonly nextAction?: string | null
  /** `true` while an escalation is open. */
  readonly isEscalated: boolean
}

const TONES: Readonly<Record<CaseStatusKind, Tone>> = {
  closed: 'grey',
  overdue: 'red',
  urgent: 'red',
  declined: 'red',
  notReached: 'amber',
  notAcknowledged: 'amber',
  needsOwner: 'amber',
  callBack: 'amber',
  waitingAcceptance: 'blue',
  onTrack: 'green',
}

function statusKind(input: CaseStatusInput): CaseStatusKind {
  const has = (name: string): boolean => input.exceptions.includes(name)
  if (input.stage === 'Closed') {
    return 'closed'
  }
  if (has('Overdue')) {
    return 'overdue'
  }
  if (isUrgent(input.urgency)) {
    return 'urgent'
  }
  if (has('Rejected')) {
    return 'declined'
  }
  if (has('Unreachable')) {
    return 'notReached'
  }
  if (has('Unacknowledged')) {
    return 'notAcknowledged'
  }
  if (has('Unassigned')) {
    return 'needsOwner'
  }
  if (input.nextAction === 'call-patient') {
    return 'callBack'
  }
  if (input.nextAction === 'respond-to-referral') {
    return 'waitingAcceptance'
  }
  return 'onTrack'
}

/**
 * Works out the status a case shows.
 *
 * @param input - The case's stage, urgency, exceptions, next action and escalation flag.
 * @returns The status word, its tone and its rank for sorting.
 *
 * @remarks
 * A call-back request is a next action (`call-patient`), not an exception, and a referral waiting
 * for the receiving unit is `respond-to-referral`: the sender stays responsible until it is
 * accepted. An open escalation is not a status: screens show it next to the status.
 */
export function caseStatus(input: CaseStatusInput): CaseStatus {
  const kind = statusKind(input)
  return {
    kind,
    tone: TONES[kind],
    label: t(`status.${kind}`),
    rank: CASE_STATUS_KINDS.indexOf(kind),
  }
}

/**
 * Tells whether a status asks something of the care team now.
 *
 * @param status - A case's status.
 * @returns `false` for cases on track, waiting on another unit or closed.
 */
export function needsAttention(status: CaseStatus): boolean {
  return status.kind !== 'onTrack' && status.kind !== 'waitingAcceptance' && status.kind !== 'closed'
}
