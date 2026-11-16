import { act } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { FhirLogPage } from '../../src/features/fhir-log';
import { fill, flush, get, renderAt } from './render';
import type { RenderResult } from './render';

describe('FhirLogPage', () => {
  let rendered: RenderResult;
  afterEach(() => {
    rendered.unmount();
    vi.unstubAllGlobals();
  });
  it('shows exact rejected request and response bodies with direction and case filters', async () => {
    const fetchMock = vi.fn<typeof fetch>();
    fetchMock.mockImplementation(
      async () =>
        new Response(
          JSON.stringify({
            entries: [
              {
                id: 'log-1',
                direction: 'sent',
                method: 'PUT',
                resourceType: 'Task',
                statusCode: 422,
                caseRef: 'run/c1',
                at: '2026-01-01T12:00:00Z',
                body: '{"resourceType":"Task"}',
                responseBody: '{"resourceType":"OperationOutcome"}',
              },
            ],
          }),
        ),
    );
    vi.stubGlobal('fetch', fetchMock);
    rendered = renderAt(<FhirLogPage />);
    await flush();
    expect(fetchMock.mock.calls.every(([url]) => String(url).includes('limit=200'))).toBe(true);
    act(() => {
      get<HTMLButtonElement>(rendered.container, 'button').click();
    });
    expect(
      Array.from(rendered.container.querySelectorAll('pre'), (element) => element.textContent),
    ).toEqual(['{"resourceType":"Task"}', '{"resourceType":"OperationOutcome"}']);
    fill(rendered.container, 'direction', 'received');
    await flush();
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('direction=received'))).toBe(
      true,
    );
    fill(rendered.container, 'caseRef', 'run/c1');
    await flush();
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('caseRef=run%2Fc1'))).toBe(
      true,
    );
  });
});
