import { useSyncExternalStore } from 'react'

/**
 * Reasons the signed-in person gave for opening cases outside their team, kept in memory only.
 *
 * @remarks
 * A team is a facility. Staff read their own facility's cases freely; the API refuses a read outside
 * it unless the request carries a `reason`, which it records with the read. The dashboard asks for
 * the reason once, then sends it with every read of that case (the case, its timeline, the
 * patient's other cases) and with an escalation acknowledgement. Reasons are never written to
 * browser storage, and they are forgotten when someone else signs in.
 */
const caseReasons = new Map<string, string>()
const listeners = new Set<() => void>()
let version = 0

function changed(): void {
  version += 1
  for (const listener of listeners) {
    listener()
  }
}

/**
 * Remembers why the signed-in person opened a case outside their team.
 *
 * @param caseId - The case.
 * @param reason - The reason they gave; blank reasons are ignored.
 */
export function rememberAccessReason(caseId: string, reason: string): void {
  const text = reason.trim()
  if (text === '' || caseReasons.get(caseId) === text) {
    return
  }
  caseReasons.set(caseId, text)
  changed()
}

/**
 * Remembers one reason for several cases, such as every case listed from another team.
 *
 * @param caseIds - The cases.
 * @param reason - The reason given; blank reasons are ignored.
 */
export function rememberAccessReasons(caseIds: readonly string[], reason: string): void {
  const text = reason.trim()
  if (text === '') {
    return
  }
  let added = false
  for (const caseId of caseIds) {
    if (caseReasons.get(caseId) !== text) {
      caseReasons.set(caseId, text)
      added = true
    }
  }
  if (added) {
    changed()
  }
}

/**
 * Looks up the reason given for a case outside the person's team.
 *
 * @param caseId - The case.
 * @returns The reason, or `undefined` when none was given.
 */
export function accessReasonFor(caseId: string): string | undefined {
  return caseReasons.get(caseId)
}

/**
 * Forgets every reason, for example when someone else signs in.
 */
export function forgetAccessReasons(): void {
  if (caseReasons.size === 0) {
    return
  }
  caseReasons.clear()
  changed()
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

function snapshot(): number {
  return version
}

/**
 * Follows the reason given for a case outside the person's team.
 *
 * @param caseId - The case.
 * @returns The reason, or `undefined` while none was given.
 *
 * @remarks
 * Re-renders the component when a reason is given or the reasons are forgotten.
 */
export function useAccessReason(caseId: string): string | undefined {
  useSyncExternalStore(subscribe, snapshot)
  return caseReasons.get(caseId)
}
