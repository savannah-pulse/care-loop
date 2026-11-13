import { t } from '../i18n'
import type { CaseStatusInput } from './status'
import { isUrgent } from './urgency'

/**
 * Says in a few plain words why a case's next action is needed, for the worklist.
 *
 * @param input - The case's stage, urgency, exceptions, next action and escalation flag.
 * @returns For example `The patient could not be reached.`, or `undefined` when nothing needs
 * explaining.
 *
 * @remarks
 * The case list carries no free-text reason, so the reason comes from what the API reports, most
 * telling first: a declined referral or handoff, a patient not reached, a message not acknowledged,
 * no named owner, a call-back request, a referral waiting for acceptance, then clinician-reviewed
 * urgency. Lateness alone is named last, because the due time already shows it.
 *
 * @example
 * ```ts
 * nextActionReason({ stage: 'FollowUpActive', exceptions: ['Unreachable'], isEscalated: false })
 * // "The patient could not be reached."
 * ```
 */
export function nextActionReason(input: CaseStatusInput): string | undefined {
  const has = (name: string): boolean => input.exceptions.includes(name)
  if (has('Rejected')) {
    return t('reasons.declined')
  }
  if (has('Unreachable')) {
    return t('reasons.notReached')
  }
  if (has('Unacknowledged')) {
    return t('reasons.notAcknowledged')
  }
  if (has('Unassigned')) {
    return t('reasons.needsOwner')
  }
  if (input.nextAction === 'call-patient') {
    return t('reasons.callBack')
  }
  if (input.nextAction === 'respond-to-referral') {
    return t('reasons.waitingAcceptance')
  }
  if (isUrgent(input.urgency)) {
    return t('reasons.urgent')
  }
  return has('Overdue') ? t('reasons.overdue') : undefined
}
