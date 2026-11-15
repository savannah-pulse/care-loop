import { useEffect, useRef, useState } from 'react';
import { simulatorRequest } from './simulator';

/**
 * Sends a user-triggered action, retaining refusals and cancelling on unmount.
 * @param baseUrl - Optional simulator base URL.
 * @returns A send function and the last submission's result, pending state and error.
 */
export function useSimulatorAction<T>(baseUrl?: string) {
  const controller = useRef<AbortController | null>(null);
  const [state, setState] = useState<{ result: T | null; pending: boolean; error: unknown }>({
    result: null,
    pending: false,
    error: null,
  });
  useEffect(
    () => () => {
      controller.current?.abort();
    },
    [],
  );
  async function send(path: string, body: unknown) {
    controller.current?.abort();
    const current = new AbortController();
    controller.current = current;
    setState({ result: null, pending: true, error: null });
    try {
      const result = await simulatorRequest<T>(baseUrl, path, current.signal, body);
      if (!current.signal.aborted) setState({ result, pending: false, error: null });
    } catch (error) {
      if (!current.signal.aborted) setState({ result: null, pending: false, error });
    }
  }
  return { ...state, send };
}
