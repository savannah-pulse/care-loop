import { afterEach, describe, expect, it, vi } from 'vitest';

import { fetchHealth } from '../../src/api';
import { simulatorRequest, SimulatorApiError } from '../../src/api';

describe('api client', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('fetchHealth returns the parsed health body', async () => {
    const body = { status: 'ok', service: 'his-simulator', synthetic: true };
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response(JSON.stringify(body), { status: 200 }))),
    );

    await expect(fetchHealth()).resolves.toEqual(body);
  });

  it('fetchHealth throws on a non-success status', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response('', { status: 503 }))),
    );

    await expect(fetchHealth()).rejects.toThrow('503');
  });

  it.each([
    ['/scenarios', undefined],
    ['/scenarios/test/run', { realSecondsPerScenarioHour: 0 }],
    ['/scenarios/runs/run-1', undefined],
    ['/staff-inbox?recipient=SYN-A', undefined],
    ['/fhir-log?direction=received&caseRef=run%2Fc1', undefined],
    ['/actions/clinical-review', { caseRef: 'run/c1', urgency: 'PLACEHOLDER-URGENCY-A' }],
  ])('calls the exact simulator route %s and forwards cancellation', async (path, body) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('{"synthetic":true}'));
    vi.stubGlobal('fetch', fetchMock);
    const controller = new AbortController();
    await expect(simulatorRequest('/his', path, controller.signal, body)).resolves.toEqual({
      synthetic: true,
    });
    expect(fetchMock).toHaveBeenCalledWith(
      '/his' + path,
      expect.objectContaining({
        signal: controller.signal,
        method: body === undefined ? 'GET' : 'POST',
      }),
    );
  });
  it('surfaces an unknown case and never returns a success-shaped fallback', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { status: 404 })));
    await expect(
      simulatorRequest('', '/actions/clinical-review', new AbortController().signal, {}),
    ).rejects.toEqual(new SimulatorApiError(404));
  });
});
