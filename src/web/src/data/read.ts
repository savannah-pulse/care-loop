import { useCallback, useEffect, useState } from 'react'
import { useSignedInStaff, type StaffMember } from '../identity'
import type { ApiFailure, ApiOutcome, FailureKind } from './outcome'

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

/**
 * How often a read that is kept up to date is repeated while the page is visible: every 30 seconds.
 *
 * @remarks
 * Often enough that a new case, a patient's reply or an escalation shows within half a minute on a
 * screen left open, and seldom enough to keep the load on the API small. The API records every
 * read of a case, so each repeat is recorded like any other read.
 */
export const LIVE_REFRESH_MS = 30_000

/**
 * How recent a read may be for the page coming back into view not to repeat it: 10 seconds.
 *
 * @remarks
 * Coming back to the tab or the window reads again straight away, unless the last answer came back
 * less than this long ago, so switching quickly between windows does not repeat a read that has only
 * just arrived.
 */
export const FRESH_READ_MS = 10_000

/** Settings for {@link useApiRead}. */
export interface ReadOptions {
  /**
   * `true` to keep the data up to date while the screen is open: the read is repeated quietly every
   * {@link LIVE_REFRESH_MS} while the page is visible, and as soon as the page is shown again or the
   * window regains focus. Off by default, so nothing is repeated unless a screen asks for it.
   */
  readonly live?: boolean
}

interface Settled<T> {
  readonly load: Loader<T>
  readonly attempt: number
  readonly person: StaffMember
  readonly refreshKey: unknown
  readonly state: ReadState<T>
}

/** Failures that can pass by themselves, so a read kept up to date tries again: no connection, or a busy API. */
const PASSING_FAILURES: readonly FailureKind[] = ['error', 'unavailable']

function worthRepeating(state: ReadState<unknown>): boolean {
  return state.status === 'ready' || (state.status === 'failed' && PASSING_FAILURES.includes(state.failure.kind))
}

function pageVisible(): boolean {
  return document.visibilityState !== 'hidden'
}

/**
 * Reads data from the API and follows the read through loading, data and failure; on request, keeps
 * the data up to date while the screen is open.
 *
 * @param load - Starts the read. Keep it stable with `useCallback`; a new function starts a new read.
 * @param refreshKey - Any value that, when it changes, starts a new read: for example a counter that
 * goes up after a change elsewhere.
 * @param options - Whether to keep the data up to date; off unless `live` is `true`.
 * @returns The current state and a `retry` function.
 *
 * @remarks
 * Side effects: calls the API when the component mounts, when `load` or `refreshKey` changes, when
 * `retry` is called and when someone signs in as another person, because what a person may see
 * depends on who they are. A read that is no longer wanted is aborted, so a slow answer never
 * overwrites a newer one, and the previous person's data is never shown to the next. A read that
 * throws instead of resolving, for example on an answer that does not match the contract, shows as
 * a failure rather than loading for ever.
 *
 * With `live`, it also polls: it reads again {@link LIVE_REFRESH_MS} after each answer while the page
 * is visible, waits while the page is hidden, and reads as soon as the page is shown again or the
 * window regains focus, unless the last answer is younger than {@link FRESH_READ_MS}. These reads are
 * quiet. The screen keeps what it shows until the new answer arrives, so nothing on it flickers,
 * moves, loses focus or is announced, and filters, open dialogs and half-filled forms stay as they
 * are. A quiet read that fails leaves the screen as it was and is tried again next time; a failure
 * shows only when there is nothing else to show. Only one read is ever on its way, and it is aborted
 * when the screen closes. A read refused, not found or not built (403, 404, 501) is not repeated by
 * itself.
 */
export function useApiRead<T>(load: Loader<T>, refreshKey?: unknown, options?: ReadOptions): Read<T> {
  const person = useSignedInStaff()
  const live = options?.live ?? false
  const [attempt, setAttempt] = useState(0)
  const [settled, setSettled] = useState<Settled<T>>()

  useEffect(() => {
    let reading: AbortController | undefined
    let timer: number | undefined
    let shown: ReadState<T> | undefined
    let answeredAt = 0

    const stopTimer = (): void => {
      window.clearTimeout(timer)
      timer = undefined
    }

    const read = (quiet: boolean): void => {
      stopTimer()
      const controller = new AbortController()
      reading = controller
      void load(controller.signal)
        .catch((): ApiOutcome<T> => ({ ok: false, failure: { kind: 'error' } }))
        .then((outcome) => {
          if (controller.signal.aborted) {
            return
          }
          reading = undefined
          answeredAt = Date.now()
          // A quiet read that failed leaves the screen as it was, with its data or its earlier failure.
          if (outcome.ok || !quiet || shown === undefined) {
            shown = outcome.ok
              ? { status: 'ready', data: outcome.data }
              : { status: 'failed', failure: outcome.failure, failedAt: new Date() }
            setSettled({ load, attempt, person, refreshKey, state: shown })
          }
          planNext()
        })
    }

    const repeatable = (): boolean =>
      live && reading === undefined && shown !== undefined && worthRepeating(shown) && pageVisible()

    const planNext = (): void => {
      stopTimer()
      if (repeatable()) {
        const wait = Math.min(LIVE_REFRESH_MS, Math.max(0, answeredAt + LIVE_REFRESH_MS - Date.now()))
        timer = window.setTimeout(() => read(true), wait)
      }
    }

    // The page was shown or hidden, or the window regained focus.
    const wake = (): void => {
      if (repeatable() && Date.now() - answeredAt >= FRESH_READ_MS) {
        read(true)
      } else {
        planNext()
      }
    }

    read(false)
    if (live) {
      document.addEventListener('visibilitychange', wake)
      window.addEventListener('focus', wake)
    }
    return () => {
      reading?.abort()
      stopTimer()
      document.removeEventListener('visibilitychange', wake)
      window.removeEventListener('focus', wake)
    }
  }, [load, attempt, person, refreshKey, live])

  const retry = useCallback(() => setAttempt((value) => value + 1), [])

  const current =
    settled !== undefined &&
    settled.load === load &&
    settled.attempt === attempt &&
    settled.person === person &&
    Object.is(settled.refreshKey, refreshKey)
  return { state: current ? settled.state : { status: 'loading' }, retry }
}
