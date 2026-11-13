/**
 * Identity: who is using the dashboard, in the prototype identity scheme.
 *
 * @remarks
 * The prototype has no real sign-in. A person is chosen from a short list in the header and the API
 * client states who is acting in the `X-Demo-User` and `X-Demo-Role` headers, which is how the API's
 * prototype identity scheme works. Real sign-in replaces this before any deployment handles real
 * patient data.
 */

export { CLINICIAN_ROLES, ROLE_KEYS, isClinicianRole, roleLabel } from './roles'
export type { RoleKey } from './roles'
export {
  DEFAULT_STAFF_MEMBER,
  STAFF,
  findStaffById,
  findStaffByReference,
  staffDescription,
} from './staff'
export type { StaffMember } from './staff'
export { SIGNED_IN_STORAGE_KEY, getSignedInStaff, signInAs, useSignedInStaff } from './store'
export { SignedInMenu } from './SignedInMenu'
