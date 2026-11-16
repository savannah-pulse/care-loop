import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Scenario, ScenarioRun } from '../../api/simulator';
import { useSimulatorAction } from '../../api/useSimulatorAction';
import { useSimulatorRead } from '../../api/useSimulatorRead';
import { ApiStatus } from '../../components/ApiStatus';

/** Props for the headless-run controls. */
export interface ScenarioRunnerPageProps {
  /** Optional simulator API base URL; omission uses the current origin. */
  apiBaseUrl?: string;
}
/** Lists scenario expectations and replays HIS steps; completion never asserts completed care. */
export function ScenarioRunnerPage({ apiBaseUrl }: ScenarioRunnerPageProps) {
  const { t } = useTranslation();
  const list = useSimulatorRead<{ scenarios: Scenario[] }>(apiBaseUrl, '/scenarios');
  const action = useSimulatorAction<ScenarioRun>(apiBaseUrl);
  const [poll, setPoll] = useState(false);
  const progress = useSimulatorRead<ScenarioRun>(
    apiBaseUrl,
    action.result === null ? null : '/scenarios/runs/' + action.result.runId,
    poll,
  );
  useEffect(() => {
    if (action.result !== null) setPoll(true);
  }, [action.result]);
  useEffect(() => {
    if (progress.data?.status === 'completed' || progress.data?.status === 'failed') setPoll(false);
  }, [progress.data?.status]);
  return (
    <section>
      <h2>{t('scenarioRunner.title')}</h2>
      <p>{t('scenarioRunner.description')}</p>
      <p>{t('scenarioRunner.boundary')}</p>
      <ApiStatus error={list.error} pending={list.pending} />
      <ul className="cards">
        {list.data?.scenarios.map((scenario) => (
          <li key={scenario.name}>
            <h3>{scenario.name}</h3>
            <p>{scenario.description}</p>
            <p>{t('scenarioRunner.steps', { count: scenario.hisStepCount })}</p>
            {scenario.expect !== null && (
              <details>
                <summary>{t('scenarioRunner.expectations')}</summary>
                <pre>{JSON.stringify(scenario.expect, null, 2)}</pre>
              </details>
            )}
            {scenario.isCohort ? (
              <p>{t('scenarioRunner.cohortDeferred')}</p>
            ) : (
              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  const values = new FormData(event.currentTarget);
                  const pacing = String(values.get('pacing') ?? '').trim();
                  const key = String(values.get('runKey') ?? '').trim();
                  void action.send('/scenarios/' + encodeURIComponent(scenario.name) + '/run', {
                    realSecondsPerScenarioHour: pacing === '' ? null : Number(pacing),
                    runKey: key || null,
                  });
                }}
              >
                <label>
                  {t('scenarioRunner.pacing')}
                  <input name="pacing" type="number" min="0" step="any" />
                </label>
                <label>
                  {t('scenarioRunner.runKey')}
                  <input name="runKey" />
                </label>
                <button
                  disabled={
                    action.pending ||
                    progress.data?.status === 'running' ||
                    progress.data?.status === 'pending'
                  }
                >
                  {t('scenarioRunner.start')}
                </button>
              </form>
            )}
          </li>
        ))}
      </ul>
      <ApiStatus error={action.error} pending={action.pending} />
      <ApiStatus error={progress.error} pending={progress.pending} />
      {(progress.data ?? action.result) !== null && (
        <div role="status">
          <h3>{t('scenarioRunner.progress')}</h3>
          <p>{t('common.status.' + (progress.data ?? action.result)?.status)}</p>
          <ul>
            {(progress.data ?? action.result)?.steps.map((step) => (
              <li key={step.stepId}>
                <strong>{step.stepId}</strong> — {t('common.status.' + step.status)} —{' '}
                <time>{step.scheduledAt}</time>
                {step.httpStatusCodes.length > 0 && (
                  <span> ({step.httpStatusCodes.join(', ')})</span>
                )}
                {step.error !== null && <p role="alert">{step.error}</p>}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
