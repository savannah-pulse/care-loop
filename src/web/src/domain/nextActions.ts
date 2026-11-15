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

/**
 * Next actions that mean sending or arranging a referral.
 *
 * @remarks
 * Before a referral goes, a clinician checks and approves the AI handoff summary that travels with
 * it, so the case screen offers clinicians the summary for these actions.
 */
export const REFERRAL_ACTIONS: readonly NextActionKey[] = ['arrange-follow-up', 'arrange-alternative-referral']

/**
 * Tells whether a next action means sending or arranging a referral.
 *
 * @param key - The next-action key from the API.
 * @returns `true` for the actions in {@link REFERRAL_ACTIONS}.
 */
export function isReferralAction(key: string | null | undefined): boolean {
  return key !== null && key !== undefined && (REFERRAL_ACTIONS as readonly string[]).includes(key)
}
