import { t } from '../i18n'

/**
 * The exception names the API reports on a case, as defined by the Domain's `CaseExceptions`.
 *
 * @remarks
 * An exception never parks a case: whenever one is set, the case also has a next action, an owner
 * and a deadline. The worklist's filter chips are these exception queues.
 */
export const EXCEPTION_NAMES = ['Overdue', 'Rejected', 'Unreachable', 'Unacknowledged', 'Unassigned'] as const

/** An exception name the dashboard knows. */
export type ExceptionName = (typeof EXCEPTION_NAMES)[number]

/**
 * Tells whether a value from the API is an exception name the dashboard knows.
 *
 * @param value - An exception name from the API.
 * @returns `true`, narrowing the value, when it is one of {@link EXCEPTION_NAMES}.
 */
export function isExceptionName(value: string): value is ExceptionName {
  return (EXCEPTION_NAMES as readonly string[]).includes(value)
}

/**
 * Names an exception in plain words.
 *
 * @param name - An exception name from the API.
 * @returns For example `Not reached` for `Unreachable`; an unknown name is returned as it is.
 */
export function exceptionLabel(name: string): string {
  return isExceptionName(name) ? t(`exceptions.${name}`) : name
}

/**
 * Explains what an exception means for the case, in one sentence.
 *
 * @param name - A known exception name.
 * @returns For example `Every way of contacting the patient failed.` for `Unreachable`.
 */
export function exceptionReason(name: ExceptionName): string {
  return t(`exceptions.why.${name}`)
}
