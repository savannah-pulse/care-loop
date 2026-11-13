import { afterEach, describe, expect, it, vi } from 'vitest';

import { fetchHealth } from '../../src/api';

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

  it.todo('listScenarios returns the scenario summaries');
  it.todo('runScenario posts to /scenarios/{name}/run and returns the run');
  it.todo('getScenarioRun returns run progress');
  it.todo('listStaffInbox passes the recipient filter');
  it.todo('listFhirLog passes the direction and caseRef filters');
  it.todo('clinician action functions post to /actions/* and surface a 404 for an unknown case');
});
