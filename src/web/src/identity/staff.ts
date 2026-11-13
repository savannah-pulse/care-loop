import { t, type MessageKey } from '../i18n'
import type { RoleKey } from './roles'

/**
 * A person someone can sign in as, in the prototype identity scheme.
 *
 * @remarks
 * The prototype has no real sign-in. Whoever uses the dashboard picks a person here, and the API is
 * told who is acting through the `X-Demo-User` and `X-Demo-Role` headers. Real sign-in replaces this
 * before any deployment handles real patient data.
 */
export interface StaffMember {
  /** Stable identifier used to remember who is signed in. */
  readonly id: string
  /** The person's reference, sent as `X-Demo-User` and compared with case owners and actors. */
  readonly reference: string
  /** The person's name as shown on screen. */
  readonly name: string
  /** Initials shown in the avatar. */
  readonly initials: string
  /** The role the person acts in, sent as `X-Demo-Role`. */
  readonly role: RoleKey
  /** Catalogue key of the person's job title. */
  readonly titleKey: MessageKey
  /** Catalogue key of the facility the person works at, when they work at one. */
  readonly facilityKey?: MessageKey
}

/**
 * The people the prototype can sign in as.
 *
 * @remarks
 * Prototype identity scheme only, to be replaced by real sign-in. Every person here is invented.
 * Their references must match the owner and actor references the API reports, so the dashboard can
 * show names instead of references.
 */
export const STAFF: readonly StaffMember[] = [
  {
    id: 'huda-saleh',
    reference: 'Practitioner/huda-saleh',
    name: 'Huda Saleh',
    initials: 'HS',
    role: 'nurse-navigator',
    titleKey: 'staffTitles.nurseNavigator',
    facilityKey: 'facilities.familyHealthCentre',
  },
  {
    id: 'rekha-varghese',
    reference: 'Practitioner/rekha-varghese',
    name: 'Dr. Rekha Varghese',
    initials: 'RV',
    role: 'clinician',
    titleKey: 'staffTitles.screeningPhysician',
    facilityKey: 'facilities.familyHealthCentre',
  },
  {
    id: 'joy-bautista',
    reference: 'Practitioner/joy-bautista',
    name: 'Joy Bautista',
    initials: 'JB',
    role: 'facility-lead',
    titleKey: 'staffTitles.facilityLead',
    facilityKey: 'facilities.familyHealthCentre',
  },
  {
    id: 'obaid-al-suwaidi',
    reference: 'Practitioner/obaid-al-suwaidi',
    name: 'Dr. Obaid Al Suwaidi',
    initials: 'OS',
    role: 'coordinator',
    titleKey: 'staffTitles.programmeCoordinator',
  },
]

/** The person signed in when nobody has been chosen yet: the nurse navigator. */
export const DEFAULT_STAFF_MEMBER: StaffMember = STAFF[0]

/**
 * Finds a person by the reference the API uses for them.
 *
 * @param reference - An owner or actor reference from the API.
 * @returns The person, or `undefined` when the reference is not one the prototype knows.
 */
export function findStaffByReference(reference: string): StaffMember | undefined {
  return STAFF.find((member) => member.reference === reference)
}

/**
 * Finds a person by their stable identifier.
 *
 * @param id - The identifier stored when someone signed in.
 * @returns The person, or `undefined` when the identifier is not known.
 */
export function findStaffById(id: string): StaffMember | undefined {
  return STAFF.find((member) => member.id === id)
}

/**
 * Describes a person's job and facility in one line.
 *
 * @param member - The person to describe.
 * @returns For example `Nurse navigator · Family Health Centre`, or just the job title.
 */
export function staffDescription(member: StaffMember): string {
  const title = t(member.titleKey)
  return member.facilityKey ? t('identity.titleAtFacility', { title, facility: t(member.facilityKey) }) : title
}
