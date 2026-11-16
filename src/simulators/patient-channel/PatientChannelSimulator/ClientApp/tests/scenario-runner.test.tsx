import { act, fireEvent, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ScenarioRunnerPage } from '../src/features/scenario-runner';
import en from '../src/locales/en.json';

import { mockApi, problem, reaction, renderAt, scenarioRun } from './helpers';

const SCENARIOS = [
  {
    name: 'cohort',
    description: 'SIMULATED cohort.',
    pathwayId: 'PLACEHOLDER-PATHWAY',
    isCohort: true,
    patientCount: 0,
    synthetic: true,
    problem: null,
  },
  {
    name: 'happy-path',
    description: 'SIMULATED journey that completes.',
    pathwayId: 'PLACEHOLDER-PATHWAY',
    isCohort: false,
    patientCount: 1,
    synthetic: true,
    problem: null,
  },
];

describe('Scenario runner', () => {
  it('lists the available scenarios', async () => {
    mockApi({ 'GET /scenarios': SCENARIOS });

    renderAt(<ScenarioRunnerPage />);

    expect(await screen.findByText('SIMULATED journey that completes.')).toBeTruthy();
    expect(screen.getByText('Patients: 1 · Pathway: PLACEHOLDER-PATHWAY')).toBeTruthy();
    expect(screen.getAllByText(en.common.simulated)).toHaveLength(2);
  });

  it('lists a scenario file that cannot be used with its problem and no way to run it', async () => {
    mockApi({
      'GET /scenarios': [
        ...SCENARIOS,
        {
          name: 'broken',
          description: '',
          pathwayId: '',
          isCohort: false,
          patientCount: 0,
          synthetic: true,
          problem: 'SIMULATED: not valid scenario JSON.',
        },
      ],
    });

    renderAt(<ScenarioRunnerPage />);

    expect(
      await screen.findByText(
        'This scenario file cannot be used: SIMULATED: not valid scenario JSON.',
      ),
    ).toBeTruthy();
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Run broken' }).disabled).toBe(
      true,
    );
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Run happy-path' }).disabled).toBe(
      false,
    );
  });

  it('starts a run and shows its run id', async () => {
    const api = mockApi({
      'GET /scenarios': SCENARIOS,
      'POST /scenarios/happy-path/run': scenarioRun(),
      'GET /scenarios/runs/run-1': scenarioRun(),
    });
    renderAt(<ScenarioRunnerPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Run happy-path' }));

    expect(await screen.findByText('Run identifier: run-1')).toBeTruthy();
    expect(screen.getByText('Patients: SYN-PT-0001')).toBeTruthy();
    expect(screen.getByTestId('run-status').textContent).toContain('Running');
    expect(api.callsTo('POST /scenarios/happy-path/run')).toHaveLength(1);
  });

  it('polls the run and shows each patient reaction', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let reads = 0;
    mockApi({
      'GET /scenarios': SCENARIOS,
      'POST /scenarios/happy-path/run': scenarioRun(),
      'GET /scenarios/runs/run-1': () => {
        reads += 1;
        return reads < 2
          ? scenarioRun()
          : scenarioRun({
              status: 'Failed',
              reactions: [
                reaction(),
                reaction({
                  reaction: 'Acknowledged',
                  succeeded: false,
                  httpStatusCode: 422,
                  error: 'CareLoop answered 422.',
                }),
              ],
            });
      },
    });
    renderAt(<ScenarioRunnerPage pollIntervalMs={1000} />);
    fireEvent.click(await screen.findByRole('button', { name: 'Run happy-path' }));
    expect(await screen.findByText(en.scenarioRunner.noReactions)).toBeTruthy();

    await act(() => vi.advanceTimersByTimeAsync(1000));

    const rows = (await screen.findAllByRole('row')).slice(1);
    expect(rows).toHaveLength(2);
    expect(rows[0]?.textContent).toContain('Delivery reported');
    expect(rows[0]?.textContent).toContain('Accepted (200)');
    expect(rows[1]?.textContent).toContain('Acknowledged');
    expect(rows[1]?.textContent).toContain('Failed: CareLoop answered 422.');
    expect(screen.getByTestId('run-status').textContent).toContain('Failed');
  });

  it('shows that nothing has happened when no notification has arrived', async () => {
    mockApi({
      'GET /scenarios': SCENARIOS,
      'POST /scenarios/happy-path/run': scenarioRun(),
      'GET /scenarios/runs/run-1': scenarioRun(),
    });
    renderAt(<ScenarioRunnerPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Run happy-path' }));

    expect(await screen.findByText(en.scenarioRunner.noReactions)).toBeTruthy();
    expect(screen.queryByRole('table')).toBeNull();
  });

  it('disables running a cohort scenario', async () => {
    const api = mockApi({ 'GET /scenarios': SCENARIOS });

    renderAt(<ScenarioRunnerPage />);

    const run = (await screen.findByRole('button', { name: 'Run cohort' })) as HTMLButtonElement;
    expect(run.disabled).toBe(true);
    expect(screen.getByText(en.scenarioRunner.cohort)).toBeTruthy();
    fireEvent.click(run);
    expect(api.calls.some((call) => call.method === 'POST')).toBe(false);
  });

  it('shows why when a run cannot be started', async () => {
    mockApi({
      'GET /scenarios': SCENARIOS,
      'POST /scenarios/happy-path/run': problem(
        500,
        'The scenario files could not be read.',
        'SIMULATED',
      ),
    });
    renderAt(<ScenarioRunnerPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Run happy-path' }));

    expect((await screen.findByRole('alert')).textContent).toContain(
      'The scenario files could not be read.',
    );
  });
});
