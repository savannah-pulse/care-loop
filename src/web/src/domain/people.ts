import { LOCALE, t } from '../i18n'
import { facilityName, humaniseId } from '../format'
import { findStaffByReference, roleLabel, staffDescription } from '../identity'
import type { Owner } from '../data'

/**
 * A person or queue described for someone reading the screen.
 *
 * @remarks
 * The API identifies people by reference, never by name. The dashboard names the people of the
 * sign-in list and describes anyone else by role and facility, so no raw reference is ever shown;
 * {@link PersonView.reference} keeps it for tracing.
 */
export interface PersonView {
  /** The name, or what stands in for it: `Dr. Rekha Varghese`, `Clinician, Family Health Centre` or `Facility lead queue`. */
  readonly name: string
  /** The line under the name, such as the job and facility; absent when the name says it all. */
  readonly detail?: string
  /** The person at the start of a sentence: `Dr. Rekha Varghese`, `A clinician at Family Health Centre` or `The facility lead queue`. */
  readonly subject: string
  /** The person inside a sentence: `Dr. Rekha Varghese`, `a clinician at Family Health Centre` or `the facility lead queue`. */
  readonly inSentence: string
  /** The reference the API gave, kept so the person can be traced, for example in a `title` attribute. */
  readonly reference: string
}

/** How much {@link describeOwner} says under a known person's name. */
export type PersonDetail = 'short' | 'long'

function roleWords(role: string): string {
  return roleLabel(role) ?? humaniseId(role)
}

function inLowerCase(words: string): string {
  return words.toLocaleLowerCase(LOCALE)
}

/**
 * Describes the owner of a case, or anyone else the API names by reference.
 *
 * @param owner - The owner from the API.
 * @param detail - `short` for the job title only, as in tables; `long` adds the facility.
 * @returns For a queue, `Facility lead queue`; for a person in the sign-in list, their name and job;
 * for anyone else, their role and facility in words, for example `Clinician, Family Health Centre`.
 *
 * @remarks
 * The API identifies owners by reference, not by name, and has no display names yet. The
 * dashboard can name the people of the prototype identity scheme; anyone else is shown by role and
 * facility until the API returns display names.
 */
export function describeOwner(owner: Owner, detail: PersonDetail = 'long'): PersonView {
  const reference = owner.reference
  if (owner.isQueue) {
    const role = inLowerCase(roleWords(owner.role))
    return {
      name: t('owner.queue', { role: roleWords(owner.role) }),
      detail: t('owner.queueDetail'),
      subject: t('owner.queueSubject', { role }),
      inSentence: t('owner.queueInSentence', { role }),
      reference,
    }
  }
  const person = findStaffByReference(reference)
  if (person) {
    return {
      name: person.name,
      detail: detail === 'long' ? staffDescription(person) : t(person.titleKey),
      subject: person.name,
      inSentence: person.name,
      reference,
    }
  }
  const facility = facilityName(owner.facilityId)
  const role = inLowerCase(roleWords(owner.role))
  return {
    name: t('owner.atFacility', { role: roleWords(owner.role), facility }),
    subject: t('owner.someoneAtStart', { role, facility }),
    inSentence: t('owner.someoneAt', { role, facility }),
    reference,
  }
}

/**
 * Names whoever caused a timeline entry.
 *
 * @param reference - The actor or source-system reference from the API.
 * @param role - The role the actor acted in; `system` for CareLoop itself.
 * @returns `CareLoop` for CareLoop's own actions, a person's name when the dashboard knows them, and
 * otherwise the role in words, for example `Receiving clinician`. Never the reference itself.
 *
 * @remarks
 * Timeline entries carry no facility, so people outside the sign-in list are named by role only.
 */
export function describeActor(reference: string, role: string): string {
  if (role === 'system') {
    return t('roles.system')
  }
  return findStaffByReference(reference)?.name ?? roleWords(role)
}
