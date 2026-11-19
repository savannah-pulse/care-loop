import { t } from '../i18n'

/** How the dashboard treats a clinician-reviewed urgency code. */
export type UrgencyLevel = 'urgent' | 'routine'

/**
 * What each urgency code means on screen.
 *
 * @remarks
 * Urgency arrives as an opaque code that the reviewing clinician set in the hospital system;
 * CareLoop never sets or changes it. `GET /programmes` lists the codes a programme accepts, but not
 * what each one means, so this one table names the placeholder codes of the breast-screening
 * pathway file and the worklist can put urgent cases first. Every shared scenario reviews results as `PLACEHOLDER-URGENCY-A`, so that code is routine.
 * A code missing from the table is shown as it is and never treated as urgent.
 */
export const URGENCY_LEVELS: Readonly<Record<string, UrgencyLevel>> = {
  'PLACEHOLDER-URGENCY-A': 'routine',
  'PLACEHOLDER-URGENCY-B': 'urgent',
}

/**
 * Looks up what an urgency code means.
 *
 * @param code - The urgency code from the API; `null` until the clinician's review arrives.
 * @returns `urgent` or `routine`, or `undefined` when there is no code or it is not known.
 */
export function urgencyLevel(code: string | null | undefined): UrgencyLevel | undefined {
  return code ? URGENCY_LEVELS[code] : undefined
}

/**
 * Tells whether a case was reviewed as urgent.
 *
 * @param code - The urgency code from the API.
 * @returns `true` only for a code known to mean urgent.
 */
export function isUrgent(code: string | null | undefined): boolean {
  return urgencyLevel(code) === 'urgent'
}

/**
 * Describes a case's urgency in plain words.
 *
 * @param code - The urgency code from the API.
 * @returns `Urgent`, `Routine`, `Not reviewed yet`, or the code itself when it is not known.
 */
export function urgencyText(code: string | null | undefined): string {
  if (!code) {
    return t('urgency.notReviewed')
  }
  const level = urgencyLevel(code)
  return level ? t(`urgency.${level}`) : code
}

/**
 * Describes a case's urgency in plain words, inside a sentence.
 *
 * @param code - The urgency code from the API.
 * @returns `urgent`, `routine` or `not reviewed yet`, or the code itself, exactly as the API sent
 * it, when it is not known.
 *
 * @remarks
 * Sentence case keeps a capital for the start of a sentence, so "For routine results" has the word
 * in lower case; a code the dashboard does not know is never changed.
 */
export function urgencyInSentence(code: string | null | undefined): string {
  if (!code) {
    return t('urgency.inSentence.notReviewed')
  }
  const level = urgencyLevel(code)
  return level ? t(`urgency.inSentence.${level}`) : code
}
