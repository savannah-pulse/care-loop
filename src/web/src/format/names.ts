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
 * CareLoop deliberately stores no patient names: the API contract says the patient reference is
 * "never a name". Names and other details stay in the hospital record. Screens keep the full
 * reference in a `title` attribute so a case can still be traced.
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

const DATED_FACT = /^(.+?)-(\d{4})-(\d{2})-(\d{2})$/
const VERSIONED_FACT = /^(.+?)-v(\d+)$/i

const factDate = new Intl.DateTimeFormat(LOCALE, {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
})

function calendarDate(year: string, month: string, day: string): Date | undefined {
  const date = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)))
  return date.getUTCMonth() === Number(month) - 1 && date.getUTCDate() === Number(day) ? date : undefined
}

/**
 * Describes a fact a handoff summary cites, from the fact's identifier.
 *
 * @param factId - A fact identifier from the summary, for example `screening-report-2026-11-10`.
 * @returns The fact in words: a trailing date becomes `, 10 Nov 2026` and a trailing `-v1` becomes
 * `, version 1`; any other identifier is put into words by {@link humaniseId}.
 *
 * @remarks
 * Summary sections cite facts by identifier only; the API has no description or date for them
 * yet. Screens keep the identifier in a `title` attribute so the fact can still be traced.
 *
 * @example
 * ```ts
 * factLabel('screening-report-2026-11-10') // "Screening report, 10 Nov 2026"
 * factLabel('breast-screening-programme-v1') // "Breast screening programme, version 1"
 * ```
 */
export function factLabel(factId: string): string {
  const dated = DATED_FACT.exec(factId)
  if (dated) {
    const [, name, year, month, day] = dated
    const date = calendarDate(year, month, day)
    if (date) {
      return t('facts.dated', { name: humaniseId(name), date: factDate.format(date) })
    }
  }
  const versioned = VERSIONED_FACT.exec(factId)
  if (versioned) {
    return t('facts.version', { name: humaniseId(versioned[1]), version: versioned[2] })
  }
  return humaniseId(factId)
}
