import { t } from '../i18n'
import { findStaffByReference, roleLabel, staffDescription } from '../identity'
import type { Owner } from '../data'

/** A person or queue described for a person reading the screen: a name and a line under it. */
export interface PersonView {
  /** The name, or what stands in for it. */
  readonly name: string
  /** The line under it: job and facility, or the reference when there is no name. */
  readonly detail: string
  /** `true` when {@link PersonView.detail} is a reference rather than words. */
  readonly detailIsReference?: boolean
}

/** How much {@link describeOwner} says under the name. */
export type PersonDetail = 'short' | 'long'

/**
 * Describes the owner of a case.
 *
 * @param owner - The owner from the API.
 * @param detail - `short` for the job title only, as in tables; `long` adds the facility.
 * @returns For a queue, `Facility lead queue`; for a person the dashboard knows, their name and job;
 * otherwise the role over the reference.
 *
 * @remarks
 * The API identifies owners by reference, not by name. The dashboard can name the people of the
 * prototype identity scheme; anyone else is shown by role and reference until the API returns
 * display names.
 */
export function describeOwner(owner: Owner, detail: PersonDetail = 'long'): PersonView {
  const role = roleLabel(owner.role)
  if (owner.isQueue) {
    return { name: t('owner.queue', { role: role ?? owner.role }), detail: t('owner.queueDetail') }
  }
  const person = findStaffByReference(owner.reference)
  if (person) {
    return { name: person.name, detail: detail === 'long' ? staffDescription(person) : t(person.titleKey) }
  }
  return {
    name: role ?? t('owner.unknownRole', { role: owner.role }),
    detail: owner.reference,
    detailIsReference: true,
  }
}

/**
 * Names whoever caused a timeline entry.
 *
 * @param reference - The actor or source-system reference from the API.
 * @param role - The role the actor acted in; `system` for CareLoop itself.
 * @returns `CareLoop` for CareLoop's own actions, a person's name when the dashboard knows them,
 * the role otherwise, or the reference as a last resort.
 */
export function describeActor(reference: string, role: string): string {
  if (role === 'system') {
    return t('roles.system')
  }
  return findStaffByReference(reference)?.name ?? roleLabel(role) ?? reference
}
