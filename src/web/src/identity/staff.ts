import { facilityName } from '../format'
import { t, type MessageKey } from '../i18n'
import type { RoleKey } from './roles'

/**
 * A person someone can sign in as, in the prototype identity scheme.
 *
 * @remarks
 * The prototype has no real sign-in. Whoever uses the dashboard picks a person here, and the API is
 * told who is acting through the `X-Demo-User`, `X-Demo-Role` and `X-Demo-Facility` headers. Real
 * sign-in replaces this before any deployment handles real patient data.
 */
export interface StaffMember {
  /** Stable identifier used to remember who is signed in. */
  readonly id: string
  /**
   * The person's reference, sent as `X-Demo-User` and compared with case owners and actors with
   * {@link sameReference}. Written without a FHIR type, the way the API stores staff.
   */
  readonly reference: string
  /** The person's name as shown on screen. */
  readonly name: string
  /** How a greeting addresses the person: the first name, with the title for doctors. */
  readonly shortName: string
  /** Initials shown in the avatar. */
  readonly initials: string
  /** The role the person acts in, sent as `X-Demo-Role`. */
  readonly role: RoleKey
  /** Catalogue key of the person's job title. */
  readonly titleKey: MessageKey
  /**
   * Identifier of the facility the person works at, sent as `X-Demo-Facility`: their team. The
   * programme coordinator works across facilities and has none.
   */
  readonly facilityId?: string
}

/**
 * The people the prototype can sign in as, listed by facility: the Family Health Centre, then the
 * General Hospital, then the programme coordinator, who works across both.
 *
 * @remarks
 * Prototype identity scheme only, to be replaced by real sign-in. Every person here is invented.
 * Their references must match the owner and actor references the API reports, so the dashboard can
 * show names instead of references. The nurse navigator comes first: she is who the dashboard signs
 * in as until someone else is chosen.
 */
export const STAFF: readonly StaffMember[] = [
  {
    id: 'huda-saleh',
    reference: 'huda-saleh',
    name: 'Huda Saleh',
    shortName: 'Huda',
    initials: 'HS',
    role: 'nurse-navigator',
    titleKey: 'staffTitles.nurseNavigator',
    facilityId: 'family-health-centre',
  },
  {
    id: 'rekha-varghese',
    reference: 'rekha-varghese',
    name: 'Dr. Rekha Varghese',
    shortName: 'Dr. Rekha',
    initials: 'RV',
    role: 'clinician',
    titleKey: 'staffTitles.screeningPhysician',
    facilityId: 'family-health-centre',
  },
  {
    id: 'sara-al-hammadi',
    reference: 'sara-al-hammadi',
    name: 'Dr. Sara Al Hammadi',
    shortName: 'Dr. Sara',
    initials: 'SH',
    role: 'clinician',
    titleKey: 'staffTitles.familyDoctor',
    facilityId: 'family-health-centre',
  },
  {
    id: 'joy-bautista',
    reference: 'joy-bautista',
    name: 'Joy Bautista',
    shortName: 'Joy',
    initials: 'JB',
    role: 'facility-lead',
    titleKey: 'staffTitles.facilityLead',
    facilityId: 'family-health-centre',
  },
  {
    id: 'khalid-noor',
    reference: 'khalid-noor',
    name: 'Khalid Noor',
    shortName: 'Khalid',
    initials: 'KN',
    role: 'lab',
    titleKey: 'staffTitles.labTechnician',
    facilityId: 'family-health-centre',
  },
  {
    id: 'mona-khalil',
    reference: 'mona-khalil',
    name: 'Mona Khalil',
    shortName: 'Mona',
    initials: 'MK',
    role: 'nurse-navigator',
    titleKey: 'staffTitles.nurseNavigator',
    facilityId: 'general-hospital',
  },
  {
    id: 'yusuf-al-mansoori',
    reference: 'yusuf-al-mansoori',
    name: 'Dr. Yusuf Al Mansoori',
    shortName: 'Dr. Yusuf',
    initials: 'YM',
    role: 'clinician',
    titleKey: 'staffTitles.screeningPhysician',
    facilityId: 'general-hospital',
  },
  {
    id: 'layla-haddad',
    reference: 'layla-haddad',
    name: 'Dr. Layla Haddad',
    shortName: 'Dr. Layla',
    initials: 'LH',
    role: 'receiving-clinician',
    titleKey: 'staffTitles.breastSurgeon',
    facilityId: 'general-hospital',
  },
  {
    id: 'anita-thomas',
    reference: 'anita-thomas',
    name: 'Anita Thomas',
    shortName: 'Anita',
    initials: 'AT',
    role: 'facility-lead',
    titleKey: 'staffTitles.facilityLead',
    facilityId: 'general-hospital',
  },
  {
    id: 'obaid-al-suwaidi',
    reference: 'obaid-al-suwaidi',
    name: 'Dr. Obaid Al Suwaidi',
    shortName: 'Dr. Obaid',
    initials: 'OS',
    role: 'coordinator',
    titleKey: 'staffTitles.programmeCoordinator',
  },
]

/** The person signed in when nobody has been chosen yet: the nurse navigator. */
export const DEFAULT_STAFF_MEMBER: StaffMember = STAFF[0]

function withoutType(reference: string): string {
  return reference
    .trim()
    .replace(/^[A-Za-z]+\//, '')
    .toLowerCase()
}

/**
 * Tells whether two staff references name the same person.
 *
 * @param a - A reference, for example an owner from the API.
 * @param b - Another reference, for example the signed-in person's.
 * @returns `true` when they match, ignoring a FHIR type in front and letter case, so
 * `rekha-varghese` matches `Practitioner/rekha-varghese`.
 *
 * @remarks
 * The API stores staff from FHIR without their type (`rekha-varghese`) but records the
 * `X-Demo-User` header as it was sent, for example in `acknowledgedBy`, so the two forms can meet
 * in one case. Every comparison of people goes through this one function, so it keeps working
 * whichever form the API settles on.
 */
export function sameReference(a: string | null | undefined, b: string | null | undefined): boolean {
  return Boolean(a && b) && withoutType(a ?? '') === withoutType(b ?? '')
}

/**
 * Finds a person by the reference the API uses for them.
 *
 * @param reference - An owner or actor reference from the API.
 * @returns The person, or `undefined` when the reference is not one the prototype knows. The
 * comparison uses {@link sameReference}.
 */
export function findStaffByReference(reference: string): StaffMember | undefined {
  return STAFF.find((member) => sameReference(member.reference, reference))
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
  return member.facilityId ? t('identity.titleAtFacility', { title, facility: facilityName(member.facilityId) }) : title
}
