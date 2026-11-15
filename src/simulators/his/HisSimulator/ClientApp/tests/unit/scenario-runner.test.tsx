import { act } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ScenarioRunnerPage } from '../../src/features/scenario-runner';
import en from '../../src/locales/en.json';
import { flush, get, renderAt } from './render';
import type { RenderResult } from './render';

describe('ScenarioRunnerPage', () => {
  let rendered: RenderResult;
  afterEach(() => {
    rendered.unmount();
    vi.unstubAllGlobals();
  });
  it('lists expectations, prevents cohort starts and polls step outcomes after explicit start', async () => {
    const run = {
      runId: 'run-1',
      status: 'completed',
      steps: [
        {
          stepId: 's1',
          status: 'rejected',
          scheduledAt: '2026-01-01T12:00:00Z',
          httpStatusCodes: [422],
          error: null,
        },
      ],
    };
    const fetchMock = vi.fn<typeof fetch>().mockImplementation(
      async (url) =>
        new Response(
          JSON.stringify(
            String(url).endsWith('/scenarios')
              ? {
                  scenarios: [
                    {
                      name: 'test',
                      description: 'Synthetic replay',
                      hisStepCount: 1,
                      isCohort: false,
                      expect: { finalStage: 'Closed' },
                    },
                    {
                      name: 'cohort',
                      description: 'Synthetic mix',
                      hisStepCount: 0,
                      isCohort: true,
                      expect: null,
                    },
                  ],
                }
              : run,
          ),
        ),
    );
    vi.stubGlobal('fetch', fetchMock);
    rendered = renderAt(<ScenarioRunnerPage />);
    await flush();
    expect(rendered.container.textContent).toContain('Closed');
    expect(rendered.container.textContent).toContain(en.scenarioRunner.cohortDeferred);
    expect(rendered.container.querySelectorAll('form')).toHaveLength(1);
    act(() => {
      get(rendered.container, 'form').dispatchEvent(
        new Event('submit', { bubbles: true, cancelable: true }),
      );
    });
    await flush();
    await flush();
    expect(
      fetchMock.mock.calls.some(
        ([url, options]) => String(url) === '/scenarios/test/run' && options?.method === 'POST',
      ),
    ).toBe(true);
    expect(fetchMock.mock.calls.some(([url]) => String(url) === '/scenarios/runs/run-1')).toBe(
      true,
    );
    expect(rendered.container.textContent).toContain('422');
    expect(rendered.container.textContent).toContain(en.common.status.rejected);
  });
  it('shows unavailable scenario service errors', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { status: 500 })));
    rendered = renderAt(<ScenarioRunnerPage />);
    await flush();
    expect(rendered.container.querySelector('[role="alert"]')?.textContent).toContain('500');
  });
});
