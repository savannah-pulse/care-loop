/**
 * Identity: who is using the dashboard, in the prototype identity scheme.
 *
 * @remarks
 * The prototype has no real sign-in. A person is chosen from a short list in the header and the API
 * client states who is acting in the `X-Demo-User`, `X-Demo-Role` and `X-Demo-Facility` headers,
 * which is how the API's prototype identity scheme works. Real sign-in replaces this before any
 * deployment handles real patient data. Reasons given for opening cases outside the person's team
 * are kept in memory for the reads that need them.
 */

export {
  accessReasonFor,
  forgetAccessReasons,
  rememberAccessReason,
  rememberAccessReasons,
  useAccessReason,
} from './access'
export {
  CLINICIAN_ROLES,
  ESCALATION_ROLES,
  NAMES_HIDDEN_ROLES,
  PATIENT_MESSAGE_ROLES,
  ROLE_KEYS,
  isCallOutcomeRole,
  isClinicianRole,
  isEscalationRole,
  isPatientMessageRole,
  namesHiddenFor,
  roleLabel,
} from './roles'
export type { RoleKey } from './roles'
export {
  DEFAULT_STAFF_MEMBER,
  STAFF,
  findStaffById,
  findStaffByReference,
  sameReference,
  staffDescription,
} from './staff'
export type { StaffMember } from './staff'
export { SIGNED_IN_STORAGE_KEY, getSignedInStaff, onSignedInChange, signInAs, useSignedInStaff } from './store'
export { SignedInMenu } from './SignedInMenu'
