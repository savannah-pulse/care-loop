import { LOCALE, t } from '../i18n'

/**
 * Facility identifiers the dashboard has a display name for.
 *
 * @remarks
 * The API identifies facilities by identifiers such as `family-health-centre` and does not share
 * their names yet. Each one listed here has its name in the language catalogue under
 * `facilities.{id}`: `family-health-centre` is Family Health Centre, `general-hospital` is General
 * Hospital and `regional-hospital` is Regional Hospital. Any other identifier is shown in words by
 * {@link humaniseId}, so a new facility still reads naturally.
 */
export const KNOWN_FACILITIES = ['family-health-centre', 'general-hospital', 'regional-hospital'] as const

/** A facility identifier the dashboard has a display name for. */
export type KnownFacility = (typeof KNOWN_FACILITIES)[number]

function isKnownFacility(id: string): id is KnownFacility {
  return (KNOWN_FACILITIES as readonly string[]).includes(id)
}

/**
 * Turns an identifier into words, in sentence case.
 *
 * @param id - An identifier such as `breast-screening-programme`.
 * @returns The words, for example `Breast screening programme`; hyphens, underscores, dots and
 * slashes become spaces. An identifier with no words is returned as it is.
 */
export function humaniseId(id: string): string {
  const words = id
    .trim()
    .split(/[-_./\s]+/)
    .filter((word) => word !== '')
    .join(' ')
  return words === '' ? id : `${words.charAt(0).toLocaleUpperCase(LOCALE)}${words.slice(1)}`
}

/**
 * Names a facility from its identifier.
 *
 * @param facilityId - The facility identifier from the API, for example `family-health-centre`.
 * @returns The facility's name, for example `Family Health Centre`, or the identifier in words when
 * the dashboard has no name for it.
 */
export function facilityName(facilityId: string): string {
  return isKnownFacility(facilityId) ? t(`facilities.${facilityId}`) : humaniseId(facilityId)
}

const PATIENT_NUMBER = /^[a-z]*[-_]?(\d+)$/i

/**
 * Groups digits in threes from the left, joined by no-break spaces.
 *
 * @param digits - A string of digits, for example `100245891`.
 * @returns For example `100 245 891`; the groups never break across lines.
 */
export function groupDigits(digits: string): string {
  return digits.match(/\d{1,3}/g)?.join('\u00a0') ?? digits
}

/**
 * Shows a patient by a friendly identifier instead of their source-system reference.
 *
 * @param reference - The patient reference from the API, for example `Patient/p-100245891`.
 * @returns `Patient 100 245 891` when the identifier after the last `/` is digits with an optional
 * letter prefix; otherwise `Patient` followed by that identifier.
 *
 * @remarks
 * The patient reference is "never a name". Staff screens show the patient's name and MRN when the
 * API sends them (see `describePatient`); this friendly number stands in when it does not. Screens
 * keep the full reference in a `title` attribute so a case can still be traced.
 *
 * @example
 * ```ts
 * patientLabel('Patient/p-100245891') // "Patient 100 245 891"
 * patientLabel('Patient/hessa') // "Patient hessa"
 * ```
 */
export function patientLabel(reference: string): string {
  const id = reference.slice(reference.lastIndexOf('/') + 1).trim() || reference
  const number = PATIENT_NUMBER.exec(id)?.[1]
  return t('patient.label', { id: number ? groupDigits(number) : id })
}
