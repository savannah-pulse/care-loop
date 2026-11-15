import { act } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { StaffInboxPage } from '../../src/features/staff-inbox';
import { fill, flush, get, renderAt } from './render';
import type { RenderResult } from './render';

describe('StaffInboxPage', () => {
  let rendered: RenderResult;
  afterEach(() => {
    rendered.unmount();
    vi.unstubAllGlobals();
  });
  it('opens addressed raw FHIR without acknowledging and encodes recipient filters', async () => {
    const item = {
      id: 'item-1',
      kind: 'handoff-proposed',
      recipientSyntheticId: 'SYN-STAFF-1',
      subject: 'SIMULATED action needed',
      logEntryId: 'log-1',
      receivedAt: '2026-01-01T12:00:00Z',
    };
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockImplementation(
        async (url) =>
          new Response(
            JSON.stringify(
              String(url).includes('/fhir-log')
                ? { entries: [{ id: 'log-1', body: '{"resourceType":"Task"}' }] }
                : String(url).endsWith('/item-1')
                  ? item
                  : { items: [item] },
            ),
          ),
      );
    vi.stubGlobal('fetch', fetchMock);
    rendered = renderAt(<StaffInboxPage />);
    await flush();
    act(() => {
      get<HTMLButtonElement>(rendered.container, 'button').click();
    });
    await flush();
    expect(rendered.container.querySelector('pre')?.textContent).toBe('{"resourceType":"Task"}');
    expect(fetchMock.mock.calls.every(([, options]) => options?.method === 'GET')).toBe(true);
    fill(rendered.container, 'recipient', 'SYN/A + B');
    await flush();
    expect(
      fetchMock.mock.calls.some(([url]) => String(url).includes('recipient=SYN%2FA%20%2B%20B')),
    ).toBe(true);
  });
  it('cancels an old filter response on navigation', async () => {
    let signal: AbortSignal | null | undefined;
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockImplementation((_url, options) => {
        signal = options?.signal;
        return new Promise(() => {});
      }),
    );
    rendered = renderAt(<StaffInboxPage />);
    rendered.unmount();
    expect(signal?.aborted).toBe(true);
    rendered = renderAt(<div />);
  });
});
