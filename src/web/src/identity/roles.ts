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
 * Roles that record what a patient said on a call: that they will come, that they said the next
 * step back, and how confident they feel.
 *
 * @remarks
 * Patients are reached by text message and email only, which cannot carry teach-back or a rating,
 * so a nurse navigator records them after a call. The API allows this role only and answers 403 to
 * anyone else; the screen shows the card to the same role.
 */
export const CALL_OUTCOME_ROLES: readonly RoleKey[] = ['nurse-navigator']

/**
 * Tells whether a role records the outcome of a call with a patient.
 *
 * @param role - A role key.
 * @returns `true` for the roles in {@link CALL_OUTCOME_ROLES}.
 */
export function isCallOutcomeRole(role: string): boolean {
  return (CALL_OUTCOME_ROLES as readonly string[]).includes(role)
}

/**
 * Roles on the escalation ladder: they are told when a step is late, and acknowledge it.
 *
 * @remarks
 * The pathway's escalation ladder goes to the facility lead, then the programme coordinator. The
 * dashboard gives these roles the Escalations screen; anyone else sees a case's escalation on the
 * case itself.
 */
export const ESCALATION_ROLES: readonly RoleKey[] = ['facility-lead', 'coordinator']

/**
 * Tells whether a role has the Escalations screen.
 *
 * @param role - A role key.
 * @returns `true` for the roles in {@link ESCALATION_ROLES}.
 */
export function isEscalationRole(role: string): boolean {
  return (ESCALATION_ROLES as readonly string[]).includes(role)
}

/**
 * Roles that never see patient names or second identifiers.
 *
 * @remarks
 * The programme coordinator sees totals with no names, and a case only while it is escalated to
 * them (who sees what, board 14). The API leaves the name and MRN out for this role; the dashboard
 * says so plainly and shows a case number instead.
 */
export const NAMES_HIDDEN_ROLES: readonly RoleKey[] = ['coordinator']

/**
 * Tells whether patient names are hidden from a role.
 *
 * @param role - A role key.
 * @returns `true` for the roles in {@link NAMES_HIDDEN_ROLES}.
 */
export function namesHiddenFor(role: string): boolean {
  return (NAMES_HIDDEN_ROLES as readonly string[]).includes(role)
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
