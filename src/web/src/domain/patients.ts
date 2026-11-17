import { t } from '../i18n'
import { groupDigits, patientLabel } from '../format'
import { namesHiddenFor } from '../identity'
import type { CaseSummary } from '../data'

/** The patient fields every case read carries: the reference always, the name and MRN for staff. */
export type PatientFields = Pick<
  CaseSummary,
  'caseId' | 'patientReference' | 'patientDisplayName' | 'patientSecondaryIdentifier'
>

/** A patient described for someone reading the screen. */
export interface PatientView {
  /** The main line: the name, `Patient 100 245 891` without one, or `Case 3F6B1C2A` when names are hidden. */
  readonly name: string
  /** The second identifier, for example `MRN 100 245 891`, or why there is none. */
  readonly identifier?: string
  /** `true` when names are hidden from the signed-in person's role. */
  readonly hidden: boolean
  /** The patient's source-system reference, for a tooltip; absent when names are hidden. */
  readonly reference?: string
  /** Everything a search box can match: the name, the MRN, the reference or the case number. */
  readonly searchText: string
}

/**
 * Shortens a case identifier to a number people can read out.
 *
 * @param caseId - CareLoop's identifier of the case, a UUID.
 * @returns Its first eight letters and digits in capitals, for example `3F6B1C2A`.
 */
export function caseNumber(caseId: string): string {
  return caseId.replace(/[^0-9a-z]/gi, '').slice(0, 8).toUpperCase()
}

/**
 * Words a patient's second identifier, such as the medical record number.
 *
 * @param identifier - The identifier from the API.
 * @returns For example `MRN 100 245 891`; digits are grouped in threes.
 */
export function secondaryIdentifierText(identifier: string): string {
  const text = identifier.trim()
  return t('patient.mrn', { mrn: /^\d+$/.test(text) ? groupDigits(text) : text })
}

/**
 * Describes the patient of a case for the signed-in person (C20).
 *
 * @param patient - The case's patient fields.
 * @param viewerRole - The role of the person reading.
 * @returns For a role names are hidden from, a case number and a plain note that names are hidden
 * for this role, with no patient identifier at all, even if the API sent one. Otherwise the name
 * with the MRN under it when the API sent them, or without a name a friendly number taken from the
 * reference, such as `Patient 100 245 891`.
 *
 * @remarks
 * Staff screens show two patient identifiers: the name and the MRN. The programme coordinator
 * never sees either, so the API leaves them out and the dashboard does not show the reference,
 * which identifies the patient too.
 *
 * @example
 * ```ts
 * describePatient({ caseId, patientReference: 'p-100245891', patientDisplayName: 'Hessa Al Dhaheri',
 *   patientSecondaryIdentifier: '100245891' }, 'nurse-navigator').identifier // "MRN 100 245 891"
 * ```
 */
export function describePatient(patient: PatientFields, viewerRole: string): PatientView {
  if (namesHiddenFor(viewerRole)) {
    const number = caseNumber(patient.caseId)
    return {
      name: t('patient.case', { number }),
      identifier: t('patient.hidden'),
      hidden: true,
      searchText: number,
    }
  }
  const name = patient.patientDisplayName?.trim()
  const identifier = patient.patientSecondaryIdentifier?.trim()
  // Every role that sees names also gets the reference; without one, the case number stands in.
  const reference = patient.patientReference ?? undefined
  if (name) {
    const identifierText = identifier ? secondaryIdentifierText(identifier) : undefined
    return {
      name,
      identifier: identifierText,
      hidden: false,
      reference,
      searchText: [name, identifier ?? '', identifierText ?? '', reference ?? ''].join(' '),
    }
  }
  const label = reference ? patientLabel(reference) : t('patient.case', { number: caseNumber(patient.caseId) })
  return {
    name: label,
    identifier: identifier ? secondaryIdentifierText(identifier) : undefined,
    hidden: false,
    reference,
    searchText: [label, identifier ?? '', reference ?? ''].join(' '),
  }
}
