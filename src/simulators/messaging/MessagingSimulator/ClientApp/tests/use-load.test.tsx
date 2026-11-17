import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { useLoad } from '../src/hooks';

/** A promise the test settles by hand, standing in for a request that is still under way. */
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}

describe('useLoad', () => {
  it('drops an answer that was requested before the value was replaced', async () => {
    const first = deferred<string>();
    const load = vi.fn(() => first.promise);
    const { result } = renderHook(() => useLoad(load));

    act(() => result.current.setData(() => 'after the action'));
    await act(async () => {
      first.resolve('before the action');
      await first.promise;
    });

    expect(result.current.data).toBe('after the action');
    expect(result.current.loading).toBe(false);
  });

  it('waits for a slow answer instead of starting the next poll over it', async () => {
    vi.useFakeTimers();
    const requests = [deferred<string>(), deferred<string>()];
    const load = vi.fn(() => (requests[load.mock.calls.length - 1] ?? deferred<string>()).promise);
    const { result } = renderHook(() => useLoad(load, 1000));

    // Far longer than the interval: the first request is still the only one.
    await act(() => vi.advanceTimersByTimeAsync(5000));
    expect(load).toHaveBeenCalledTimes(1);

    await act(async () => {
      requests[0]?.resolve('slow answer');
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(result.current.data).toBe('slow answer');

    await act(() => vi.advanceTimersByTimeAsync(1000));
    expect(load).toHaveBeenCalledTimes(2);
    vi.useRealTimers();
  });

  it('keeps polling after a failure', async () => {
    vi.useFakeTimers();
    const load = vi
      .fn<() => Promise<string>>()
      .mockRejectedValueOnce(new Error('SIMULATED outage'))
      .mockResolvedValue('recovered');
    const { result } = renderHook(() => useLoad(load, 1000));

    await act(() => vi.advanceTimersByTimeAsync(0));
    expect(result.current.error).toBe('SIMULATED outage');

    await act(() => vi.advanceTimersByTimeAsync(1000));
    vi.useRealTimers();
    await waitFor(() => expect(result.current.data).toBe('recovered'));
    expect(result.current.error).toBeNull();
  });
});
