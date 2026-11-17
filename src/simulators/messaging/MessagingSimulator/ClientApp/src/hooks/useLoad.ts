import { useCallback, useEffect, useRef, useState } from 'react';

/** What {@link useLoad} gives a screen. */
export interface LoadState<T> {
  /** The latest loaded value, or `undefined` before the first load succeeds. */
  data: T | undefined;
  /** Readable description of the latest failure, or `null` when the latest load succeeded. */
  error: string | null;
  /** Whether the first load is still in progress. */
  loading: boolean;
  /** Loads again now. */
  reload: () => void;
  /** Replaces the loaded value without a request, for example with the answer of an action. */
  setData: (update: (current: T | undefined) => T | undefined) => void;
}

/**
 * Turns anything thrown into text for the screen.
 *
 * @param error - The thrown value.
 * @returns Its message when it is an `Error`, otherwise its string form.
 */
export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

/**
 * Loads a value from the simulator's host when the screen appears, and optionally keeps it fresh.
 *
 * @remarks
 * Makes a network call on mount, on every `reload()` and, when `pollIntervalMs` is given, again
 * that long after each request has settled. Waiting for the answer before scheduling the next
 * request means a slow host never has its answers thrown away by the next poll. An answer is
 * dropped when the screen has gone, and when `setData` replaced the value while the request was
 * under way: that answer describes the state before the change, and showing it would undo what
 * the screen just displayed. The next poll brings the current state.
 *
 * @param load - Performs the request. It must keep its identity between renders (a module-level function or a `useCallback`).
 * @param pollIntervalMs - How long to wait after one request before loading again, in milliseconds. Polling is off when omitted or zero.
 * @returns The loaded value with its loading and error state.
 */
export function useLoad<T>(load: () => Promise<T>, pollIntervalMs?: number): LoadState<T> {
  const [data, setValue] = useState<T | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [tick, setTick] = useState(0);
  // Counts the times setData replaced the value, so a request can tell it started before one.
  const changes = useRef(0);

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;

    const run = () => {
      const changesAtStart = changes.current;
      load()
        .then((value) => {
          if (!cancelled && changes.current === changesAtStart) {
            setValue(value);
            setError(null);
          }
        })
        .catch((failure: unknown) => {
          if (!cancelled) {
            setError(errorMessage(failure));
          }
        })
        .finally(() => {
          if (!cancelled) {
            setLoading(false);
            if (pollIntervalMs) {
              timer = setTimeout(run, pollIntervalMs);
            }
          }
        });
    };

    run();
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [load, tick, pollIntervalMs]);

  const reload = useCallback(() => setTick((value) => value + 1), []);
  const setData = useCallback((update: (current: T | undefined) => T | undefined) => {
    changes.current += 1;
    setValue((current) => update(current));
  }, []);

  return { data, error, loading, reload, setData };
}
