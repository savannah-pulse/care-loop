import { useCallback, useEffect, useState } from 'react'
import { useSignedInStaff, type StaffMember } from '../identity'
import type { ApiFailure, ApiOutcome } from './outcome'

/**
 * Where a read from the API stands.
 *
 * @remarks
 * Screens show a different state for each: a calm "loading" line, the data, a calm "not available
 * yet" when the endpoint answers 501, and an error with a way to try again for anything else.
 */
export type ReadState<T> =
  | { readonly status: 'loading' }
  | { readonly status: 'ready'; readonly data: T }
  | { readonly status: 'failed'; readonly failure: ApiFailure; readonly failedAt: Date }

/** A read from the API and a way to repeat it. */
export interface Read<T> {
  /** Where the read stands. */
  readonly state: ReadState<T>
  /** Starts the read again, for example after a failure. */
  readonly retry: () => void
}

/** Starts one read. The signal is aborted when the result is no longer wanted. */
export type Loader<T> = (signal: AbortSignal) => Promise<ApiOutcome<T>>

interface Settled<T> {
  readonly load: Loader<T>
  readonly attempt: number
  readonly person: StaffMember
  readonly refreshKey: unknown
  readonly state: ReadState<T>
}

/**
 * Reads data from the API and follows the read through loading, data and failure.
 *
 * @param load - Starts the read. Keep it stable with `useCallback`; a new function starts a new read.
 * @param refreshKey - Any value that, when it changes, starts a new read: for example a counter that
 * goes up after a change elsewhere.
 * @returns The current state and a `retry` function.
 *
 * @remarks
 * Side effects: calls the API when the component mounts, when `load` or `refreshKey` changes, when
 * `retry` is called and when someone signs in as another person, because what a person may see
 * depends on who they are. A read that is no longer wanted is aborted, so a slow answer never
 * overwrites a newer one, and the previous person's data is never shown to the next. A read that
 * throws instead of resolving, for example on an answer that does not match the contract, shows as
 * a failure rather than loading for ever.
 */
export function useApiRead<T>(load: Loader<T>, refreshKey?: unknown): Read<T> {
  const person = useSignedInStaff()
  const [attempt, setAttempt] = useState(0)
  const [settled, setSettled] = useState<Settled<T>>()

  useEffect(() => {
    const controller = new AbortController()
    void load(controller.signal)
      .catch((): ApiOutcome<T> => ({ ok: false, failure: { kind: 'error' } }))
      .then((outcome) => {
        if (controller.signal.aborted) {
          return
        }
        setSettled({
          load,
          attempt,
          person,
          refreshKey,
          state: outcome.ok
            ? { status: 'ready', data: outcome.data }
            : { status: 'failed', failure: outcome.failure, failedAt: new Date() },
        })
      })
    return () => controller.abort()
  }, [load, attempt, person, refreshKey])

  const retry = useCallback(() => setAttempt((value) => value + 1), [])

  const current =
    settled !== undefined &&
    settled.load === load &&
    settled.attempt === attempt &&
    settled.person === person &&
    Object.is(settled.refreshKey, refreshKey)
  return { state: current ? settled.state : { status: 'loading' }, retry }
}
