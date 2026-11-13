import { hasMessage, t } from '../i18n'

/**
 * Role keys a person can act in, as sent in the `X-Demo-Role` header.
 *
 * @remarks
 * They must match the role keys in the pathway definition, which is what the API's rules check.
 * `nurse-navigator`, `receiving-clinician` and `lab` are planned in the API's role list; the
 * pathway file already uses them.
 */
export const ROLE_KEYS = [
  'clinician',
  'receiving-clinician',
  'nurse-navigator',
  'coordinator',
  'facility-lead',
  'lab',
] as const

/** A role key a person can act in. */
export type RoleKey = (typeof ROLE_KEYS)[number]

/**
 * Roles that check and approve AI handoff summaries.
 *
 * @remarks
 * Which roles may approve is still open in the API design (proposed as `summaryApproverRoles` in the
 * pathway definition). The screen offers the summary to these roles; the API decides and answers
 * 403 to anyone else.
 */
export const CLINICIAN_ROLES: readonly RoleKey[] = ['clinician', 'receiving-clinician']

/**
 * Tells whether a role checks and approves handoff summaries.
 *
 * @param role - A role key from the API or the signed-in person.
 * @returns `true` for the clinician roles in {@link CLINICIAN_ROLES}.
 */
export function isClinicianRole(role: string): boolean {
  return (CLINICIAN_ROLES as readonly string[]).includes(role)
}

/**
 * Names a role in plain words.
 *
 * @param role - A role key, for example `facility-lead`.
 * @returns The role's name, for example `Facility lead`, or `undefined` when the role is not known.
 */
export function roleLabel(role: string): string | undefined {
  const key = `roles.${role}`
  return hasMessage(key) ? t(key) : undefined
}
