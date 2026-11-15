import { useEffect, useState } from 'react';
import { simulatorRequest } from './simulator';

/**
 * Reads a simulator route and cancels stale requests.
 * @param baseUrl - Optional simulator base URL.
 * @param path - Relative route; null suspends reads.
 * @param poll - Whether to poll every second without overlapping requests.
 * @param refresh - Change this value to refresh after a manual action.
 * @returns Current data, pending state and explicit error.
 */
export function useSimulatorRead<T>(
  baseUrl: string | undefined,
  path: string | null,
  poll = false,
  refresh = 0,
) {
  const [state, setState] = useState<{ data: T | null; pending: boolean; error: unknown }>({
    data: null,
    pending: true,
    error: null,
  });
  useEffect(() => {
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;
    setState({ data: null, pending: path !== null, error: null });
    async function read() {
      if (path === null) return;
      try {
        const data = await simulatorRequest<T>(baseUrl, path, controller.signal);
        if (!controller.signal.aborted) setState({ data, pending: false, error: null });
      } catch (error) {
        if (!controller.signal.aborted) setState({ data: null, pending: false, error });
      }
      if (poll && !controller.signal.aborted)
        timer = setTimeout(() => {
          void read();
        }, 1000);
    }
    void read();
    return () => {
      controller.abort();
      clearTimeout(timer);
    };
  }, [baseUrl, path, poll, refresh]);
  return state;
}
