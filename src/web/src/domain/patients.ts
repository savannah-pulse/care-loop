import { t } from '../i18n'
import { groupDigits, patientLabel } from '../format'
import { namesHiddenFor } from '../identity'
import type { CaseSummary } from '../data'

/**
 * The patient fields a case read carries: for staff the reference, name and MRN; the API leaves all
 * three out for the programme coordinator.
 */
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

/**
 * Says where a case's result came from, for the person reading.
 *
 * @param sourceResultId - The hospital system's identifier of the result, as the API sent it; the
 * API leaves it out for the programme coordinator.
 * @param viewerRole - The role of the person reading.
 * @returns `Result DiagnosticReport/r-100245891 from the hospital system` for staff, so the result
 * can be found in the hospital record; `Result from the hospital system`, without the number, for
 * a role names are hidden from, or when the API sent no number.
 *
 * @remarks
 * The hospital system's own numbers point to the patient as surely as the MRN does, so the
 * programme coordinator never sees them, even when the API sends them.
 */
export function resultSourceText(sourceResultId: string | null | undefined, viewerRole: string): string {
  const result = sourceResultId?.trim()
  return namesHiddenFor(viewerRole) || !result ? t('case.resultFromHidden') : t('case.resultFrom', { result })
}

/**
 * Says what outcome evidence a case has, for the person reading.
 *
 * @param reference - The hospital system's reference of the outcome evidence, as the API sent it.
 * @param viewerRole - The role of the person reading.
 * @returns `Outcome evidence: Procedure/…` for staff; for a role names are hidden from, that the
 * evidence is recorded in the hospital system, without its reference, which points to the patient.
 */
export function outcomeEvidenceText(reference: string, viewerRole: string): string {
  return namesHiddenFor(viewerRole) ? t('case.outcomeEvidenceHidden') : t('case.outcomeEvidence', { reference })
}

/** What {@link caseLinkNames} needs to know about each case in a list. */
export interface CaseLinkSubject {
  /** CareLoop's identifier of the case. */
  readonly caseId: string
  /** The case's patient, as {@link describePatient} describes them to the reader. */
  readonly patient: PatientView
  /** The name of the case's programme, for example `Breast screening`. */
  readonly programme: string
}

/**
 * Names the links to the cases in a list, so that each starts with the patient as shown on screen
 * and no two are the same.
 *
 * @param cases - The cases in the list, in any order.
 * @returns One name per case, in the same order: the patient's name with their MRN, for example
 * `Hessa Al Dhaheri, MRN 100 245 891`, or just what is shown where there is no MRN, such as the
 * case number where names are hidden. Where two cases would still share a name, as when one
 * patient has two open cases, the programme is added, then the case number.
 *
 * @remarks
 * Screen-reader users move between links by their names, so no two links to different cases may
 * read the same, and each name starts with the words on screen, so people using speech can say
 * what they see (WCAG 2.5.3, label in name).
 *
 * @example
 * ```ts
 * caseLinkNames([{ caseId, patient: describePatient(summary, 'nurse-navigator'), programme: 'Breast screening' }])
 * // ["Hessa Al Dhaheri, MRN 100 245 891"]
 * ```
 */
export function caseLinkNames(cases: readonly CaseLinkSubject[]): string[] {
  const separator = t('common.listSeparator')
  const names = cases.map((item) =>
    !item.patient.hidden && item.patient.identifier ? [item.patient.name, item.patient.identifier] : [item.patient.name],
  )
  const tiebreakers: readonly ((item: CaseLinkSubject) => string)[] = [
    (item) => item.programme,
    (item) => t('patient.case', { number: caseNumber(item.caseId) }),
  ]
  for (const tiebreaker of tiebreakers) {
    const counts = new Map<string, number>()
    for (const parts of names) {
      const name = parts.join(separator)
      counts.set(name, (counts.get(name) ?? 0) + 1)
    }
    names.forEach((parts, index) => {
      const extra = tiebreaker(cases[index])
      if ((counts.get(parts.join(separator)) ?? 0) > 1 && !parts.includes(extra)) {
        parts.push(extra)
      }
    })
  }
  return names.map((parts) => parts.join(separator))
}
