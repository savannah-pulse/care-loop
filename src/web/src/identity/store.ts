import { useSyncExternalStore } from 'react'
import { forgetAccessReasons } from './access'
import { DEFAULT_STAFF_MEMBER, findStaffById, type StaffMember } from './staff'

/** Browser storage key under which the signed-in person is remembered. */
export const SIGNED_IN_STORAGE_KEY = 'careloop.signedInAs'

function readStoredMember(): StaffMember {
  try {
    const id = window.localStorage.getItem(SIGNED_IN_STORAGE_KEY)
    return (id && findStaffById(id)) || DEFAULT_STAFF_MEMBER
  } catch {
    return DEFAULT_STAFF_MEMBER
  }
}

let signedIn: StaffMember = readStoredMember()
const listeners = new Set<() => void>()

function notify(): void {
  for (const listener of listeners) {
    listener()
  }
}

/**
 * Returns the person currently signed in.
 *
 * @returns The signed-in person; the nurse navigator until someone else is chosen.
 *
 * @remarks
 * Prototype identity scheme, to be replaced by real sign-in. The API client calls this on every
 * request to state who is acting.
 */
export function getSignedInStaff(): StaffMember {
  return signedIn
}

/**
 * Signs in as another person and remembers the choice in this browser.
 *
 * @param id - The identifier of a person in the prototype's staff list.
 *
 * @remarks
 * Prototype identity scheme, to be replaced by real sign-in. Unknown identifiers are ignored.
 * Every screen reading data refetches, because what a person may see depends on who they are, and
 * the reasons given for cases outside the previous person's team are forgotten.
 */
export function signInAs(id: string): void {
  const member = findStaffById(id)
  if (!member || member === signedIn) {
    return
  }
  signedIn = member
  forgetAccessReasons()
  try {
    window.localStorage.setItem(SIGNED_IN_STORAGE_KEY, member.id)
  } catch {
    // Storage can be blocked; the choice then lasts until the page is reloaded.
  }
  notify()
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  const onStorage = (event: StorageEvent): void => {
    if (event.key === SIGNED_IN_STORAGE_KEY) {
      signedIn = readStoredMember()
      forgetAccessReasons()
      notify()
    }
  }
  window.addEventListener('storage', onStorage)
  return () => {
    listeners.delete(listener)
    window.removeEventListener('storage', onStorage)
  }
}

/**
 * Follows the person currently signed in.
 *
 * @returns The signed-in person.
 *
 * @remarks
 * Prototype identity scheme, to be replaced by real sign-in. Re-renders the component when someone
 * signs in as another person, in this tab or another one.
 */
export function useSignedInStaff(): StaffMember {
  return useSyncExternalStore(subscribe, getSignedInStaff)
}
