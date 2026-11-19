import type { CaseDetail } from '../../data'
import { describeOwner, nextActionRole } from '../../domain'
import { roleLabel, sameReference } from '../../identity'
import { LOCALE, t } from '../../i18n'

/** The person reading the case: whose reference and role decide whether a step is theirs. */
export interface WhoActsViewer {
  /** The signed-in person's reference. */
  readonly reference: string
  /** The signed-in person's role key. */
  readonly role: string
}

/**
 * Says who takes the case's next step, and how it is done, for the line under the next-action card.
 *
 * @param detail - The open case, from `GET /cases/{caseId}`.
 * @param me - The person reading.
 * @returns One or two sentences. To a nurse navigator, for example: Your step: call Hessa Al Dhaheri,
 * then record it in "After a call with the patient". Dr. Rekha Varghese stays responsible for the case.
 *
 * @remarks
 * A nurse navigator's step is a call: it is made by phone and recorded on the case screen, so a
 * nurse navigator is told to make it, and anyone else is told that a nurse navigator makes it.
 * Every other step is done in the hospital system, and CareLoop moves on when the hospital system
 * tells it. The person who takes such a step is the proposed owner while a transfer to them waits,
 * the owner when the step is in their role, and otherwise the step's role, such as the facility
 * lead who chooses an owner. Whenever someone other than the person responsible acts, the line
 * also says who stays responsible, because taking the step never changes that; a nurse navigator
 * who is also the person responsible is just told to make the call. A step the
 * dashboard has no words for keeps the plain "who needs to do this".
 */
export function whoActs(detail: CaseDetail, me: WhoActsViewer): string {
  const role = nextActionRole(detail.nextAction)
  const owner = describeOwner(detail.owner)
  const ownerIsMe = !detail.owner.isQueue && sameReference(detail.owner.reference, me.reference)
  if (role === undefined) {
    return ownerIsMe ? t('case.youAct') : t('case.otherActs', { name: owner.subject })
  }
  const staysResponsible = detail.owner.isQueue
    ? ''
    : ` ${ownerIsMe ? t('case.whoActs.youStayResponsible') : t('case.whoActs.staysResponsible', { name: owner.subject })}`

  if (role === 'nurse-navigator') {
    if (me.role === 'nurse-navigator') {
      const patient = detail.patientDisplayName ?? t('case.whoActs.thePatient')
      return t('case.whoActs.youCall', { patient, card: t('callOutcome.title') }) + (ownerIsMe ? '' : staysResponsible)
    }
    return t('case.whoActs.nurseCalls') + staysResponsible
  }

  const proposed = detail.proposedOwner
  if (proposed && proposed.role === role) {
    return !proposed.isQueue && sameReference(proposed.reference, me.reference)
      ? t('case.whoActs.youInHospital')
      : t('case.whoActs.otherInHospital', { name: describeOwner(proposed).subject })
  }
  if (!detail.owner.isQueue && detail.owner.role === role) {
    return ownerIsMe ? t('case.whoActs.youInHospital') : t('case.whoActs.otherInHospital', { name: owner.subject })
  }
  if (me.role === role) {
    return t('case.whoActs.youInHospital') + staysResponsible
  }
  const roleWords = (roleLabel(role) ?? role).toLocaleLowerCase(LOCALE)
  return t('case.whoActs.roleInHospital', { role: roleWords }) + staysResponsible
}
