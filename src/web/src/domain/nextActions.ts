import { t } from '../i18n'

/**
 * The next-action keys of the breast-screening pathway file, each with plain words on screen.
 *
 * @remarks
 * The API sends the key of the single next action. The words for each key live in the language
 * catalogue; a key the dashboard does not know is shown as the key itself.
 */
export const NEXT_ACTION_KEYS = [
  'assign-owner',
  'review-result',
  'acknowledge-notification',
  'answer-consultation',
  'respond-to-referral',
  'arrange-follow-up',
  'call-patient',
  'contact-patient-another-way',
  'reassign-consultation',
  'arrange-alternative-referral',
  'rebook-appointment',
  'start-treatment',
  'decide-closure',
] as const

/** A next-action key the dashboard has words for. */
export type NextActionKey = (typeof NEXT_ACTION_KEYS)[number]

/**
 * Tells whether a next-action key from the API is one the dashboard has words for.
 *
 * @param key - The next-action key from the API.
 * @returns `true`, narrowing the key, when it is one of {@link NEXT_ACTION_KEYS}.
 */
export function isNextActionKey(key: string): key is NextActionKey {
  return (NEXT_ACTION_KEYS as readonly string[]).includes(key)
}

/**
 * The role that takes each next action, as the deadlines in the pathway file say (their `ownerRole`).
 *
 * @remarks
 * The person responsible for a case does not always take its next step: a nurse navigator makes the
 * calls on a clinician's case, and the receiving clinician answers a referral that the sender stays
 * responsible for until it is accepted. A nurse navigator's step is done by phone and recorded in
 * CareLoop; every other step is done in the hospital system.
 */
export const NEXT_ACTION_ROLES: Readonly<Record<NextActionKey, string>> = {
  'assign-owner': 'facility-lead',
  'review-result': 'clinician',
  'acknowledge-notification': 'nurse-navigator',
  'answer-consultation': 'receiving-clinician',
  'respond-to-referral': 'receiving-clinician',
  'arrange-follow-up': 'clinician',
  'call-patient': 'nurse-navigator',
  'contact-patient-another-way': 'nurse-navigator',
  'reassign-consultation': 'clinician',
  'arrange-alternative-referral': 'clinician',
  'rebook-appointment': 'receiving-clinician',
  'start-treatment': 'receiving-clinician',
  'decide-closure': 'receiving-clinician',
}

/**
 * Tells which role takes a next action.
 *
 * @param key - The next-action key from the API; `null` when there is none.
 * @returns The role key from {@link NEXT_ACTION_ROLES}, or `undefined` for no action or a key the
 * dashboard does not know.
 */
export function nextActionRole(key: string | null | undefined): string | undefined {
  return key && isNextActionKey(key) ? NEXT_ACTION_ROLES[key] : undefined
}

/**
 * Describes a case's next action in plain words.
 *
 * @param key - The next-action key from the API; `null` when there is none.
 * @returns For example `Send a new referral` for `arrange-alternative-referral`.
 */
export function nextActionText(key: string | null | undefined): string {
  if (!key) {
    return t('nextActions.none')
  }
  return isNextActionKey(key) ? t(`nextActions.${key}`) : t('nextActions.unknown', { key })
}
