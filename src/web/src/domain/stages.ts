import { t } from '../i18n'
import type { CaseDisplayLabel } from '../data'

/**
 * The ten display stages, in the order a case moves through them.
 *
 * @remarks
 * Every case, in every programme, shows one of these ten labels. The API works the label out from
 * the case's five-stage pathway position and its four tracks (decision 19) and sends it with every
 * case read; the dashboard shows what it is given and never works it out itself.
 */
export const DISPLAY_LABELS: readonly CaseDisplayLabel[] = [
  'ResultReceived',
  'CaseOpened',
  'OwnerAssigned',
  'PatientNotified',
  'PatientAcknowledged',
  'Consultation',
  'FollowUp',
  'OutcomeRecorded',
  'ClosureReview',
  'Closed',
]

/**
 * Names a display stage in plain words.
 *
 * @param label - The display label from the API.
 * @returns For example `Follow-up` for `FollowUp`.
 */
export function displayLabelText(label: CaseDisplayLabel): string {
  return t(`stages.labels.${label}`)
}

/**
 * Tells which of the ten display stages a label is.
 *
 * @param label - The display label from the API.
 * @returns Its zero-based position in {@link DISPLAY_LABELS}, for example `6` for `FollowUp`; `-1`
 * for a label the dashboard does not know.
 */
export function displayStepIndex(label: CaseDisplayLabel): number {
  return DISPLAY_LABELS.indexOf(label)
}

/**
 * Tells whether a value from the API is one of the ten display labels.
 *
 * @param value - A key from the API, such as a key of `casesByDisplayLabel`.
 * @returns `true`, narrowing the value, when it is one of {@link DISPLAY_LABELS}.
 */
export function isDisplayLabel(value: string): value is CaseDisplayLabel {
  return (DISPLAY_LABELS as readonly string[]).includes(value)
}
