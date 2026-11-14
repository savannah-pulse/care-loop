import { useCallback, useEffect, useState } from 'react';

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
 * Makes a network call on mount, on every `reload()` and, when `pollIntervalMs` is given, on that
 * interval. An answer that arrives after the screen has gone, or after a newer request started,
 * is dropped.
 *
 * @param load - Performs the request. It must keep its identity between renders (a module-level function or a `useCallback`).
 * @param pollIntervalMs - How often to load again, in milliseconds. Polling is off when omitted or zero.
 * @returns The loaded value with its loading and error state.
 */
export function useLoad<T>(load: () => Promise<T>, pollIntervalMs?: number): LoadState<T> {
  const [data, setValue] = useState<T | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [tick, setTick] = useState(0);

  useEffect(() => {
    let cancelled = false;
    load()
      .then((value) => {
        if (!cancelled) {
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
        }
      });

    return () => {
      cancelled = true;
    };
  }, [load, tick]);

  useEffect(() => {
    if (!pollIntervalMs) {
      return undefined;
    }

    const timer = setInterval(() => setTick((value) => value + 1), pollIntervalMs);
    return () => clearInterval(timer);
  }, [pollIntervalMs]);

  const reload = useCallback(() => setTick((value) => value + 1), []);
  const setData = useCallback(
    (update: (current: T | undefined) => T | undefined) => setValue((current) => update(current)),
    [],
  );

  return { data, error, loading, reload, setData };
}
