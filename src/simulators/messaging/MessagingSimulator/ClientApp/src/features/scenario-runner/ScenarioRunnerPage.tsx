import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { getScenarioRun, listScenarios, runScenario } from '../../api';
import type { PatientReactionRecord, ScenarioRunState } from '../../api';
import { SimulatedBadge } from '../../components';
import { formatTime } from '../../format';
import { errorMessage, useLoad } from '../../hooks';

/** Props for {@link ScenarioRunnerPage}. */
export interface ScenarioRunnerPageProps {
  /** How often to re-read a run in progress, in milliseconds. Polling is off when omitted. */
  pollIntervalMs?: number;
}

/** Props for {@link RunPanel}. */
interface RunPanelProps {
  /** Identifier of the run to show. */
  runId: string;
  /** The run's state when it was started, shown until the first re-read arrives. */
  initial: ScenarioRunState;
  /** How often to re-read the run, in milliseconds. Polling is off when omitted. */
  pollIntervalMs?: number;
}

/**
 * One scenario run: its patients and every reaction so far.
 *
 * @remarks
 * Re-reads the run on the polling interval, because reactions appear only when CareLoop
 * notifies one of the run's patients. An empty list means no notification has arrived.
 */
function RunPanel({ runId, initial, pollIntervalMs }: RunPanelProps) {
  const { t } = useTranslation();
  const load = useCallback(() => getScenarioRun(runId), [runId]);
  const { data, error } = useLoad(load, pollIntervalMs);
  const run = data ?? initial;

  const result = (reaction: PatientReactionRecord) => {
    if (!reaction.succeeded) {
      return t('scenarioRunner.failed', { error: reaction.error ?? reaction.httpStatusCode ?? '' });
    }

    return reaction.httpStatusCode === null
      ? t('scenarioRunner.done')
      : t('scenarioRunner.accepted', { status: reaction.httpStatusCode });
  };

  return (
    <article className="panel" aria-labelledby="run-title">
      <h3 id="run-title">{t('scenarioRunner.runHeading')}</h3>
      <SimulatedBadge />
      <ul className="plain-list">
        <li>{t('scenarioRunner.runId', { runId: run.runId })}</li>
        <li>{t('scenarioRunner.runScenario', { name: run.scenarioName })}</li>
        <li data-testid="run-status">
          {t('scenarioRunner.runStatus', { status: t(`scenarioRunner.status.${run.status}`) })}
        </li>
        <li>{t('scenarioRunner.runStarted', { time: formatTime(run.startedAt) })}</li>
        <li>{t('scenarioRunner.runPatients', { patients: run.patientSyntheticIds.join(', ') })}</li>
      </ul>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}

      {run.reactions.length === 0 ? (
        <p>{t('scenarioRunner.noReactions')}</p>
      ) : (
        <table aria-label={t('scenarioRunner.reactionsHeading')}>
          <thead>
            <tr>
              <th scope="col">{t('scenarioRunner.columns.at')}</th>
              <th scope="col">{t('scenarioRunner.columns.patient')}</th>
              <th scope="col">{t('scenarioRunner.columns.reaction')}</th>
              <th scope="col">{t('scenarioRunner.columns.source')}</th>
              <th scope="col">{t('scenarioRunner.columns.result')}</th>
            </tr>
          </thead>
          <tbody>
            {run.reactions.map((reaction, index) => (
              <tr key={`${reaction.messageId}-${reaction.reaction}-${index}`}>
                <td>{formatTime(reaction.at)}</td>
                <td>{reaction.patientSyntheticId}</td>
                <td>{t(`scenarioRunner.reaction.${reaction.reaction}`)}</td>
                <td>
                  {reaction.automatic ? t('scenarioRunner.automatic') : t('scenarioRunner.manual')}
                </td>
                <td>{result(reaction)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </article>
  );
}

/**
 * Lists the shared scenarios, puts the patient behaviours of one in force and shows the reactions.
 *
 * @remarks
 * A run sends nothing by itself. It sets how the scenario's patients behave; they then react
 * only when CareLoop notifies them, which happens because the HIS simulator replays the same
 * scenario's hospital-system events. A cohort scenario cannot be run here, because the messaging
 * simulator puts the behaviours of named patients in force.
 */
export function ScenarioRunnerPage({ pollIntervalMs }: ScenarioRunnerPageProps) {
  const { t } = useTranslation();
  const scenarios = useLoad(listScenarios);
  const [run, setRun] = useState<ScenarioRunState | null>(null);
  const [runError, setRunError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const start = async (name: string) => {
    setBusy(true);
    setRunError(null);
    try {
      setRun(await runScenario(name));
    } catch (failure) {
      setRunError(errorMessage(failure));
    } finally {
      setBusy(false);
    }
  };

  const error = scenarios.error ?? runError;

  return (
    <section aria-labelledby="runner-title">
      <h2 id="runner-title">{t('scenarioRunner.title')}</h2>
      <p>{t('scenarioRunner.description')}</p>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}

      <article className="panel" aria-labelledby="scenario-list-title">
        <h3 id="scenario-list-title">{t('scenarioRunner.listHeading')}</h3>
        {scenarios.loading && <p>{t('common.loading')}</p>}
        {scenarios.data?.length === 0 && <p>{t('scenarioRunner.empty')}</p>}
        <ul className="scenario-list">
          {(scenarios.data ?? []).map((scenario) => (
            <li key={scenario.name}>
              <div>
                <strong>{scenario.name}</strong> <SimulatedBadge />
                <p>{scenario.description}</p>
                {scenario.problem && (
                  <p className="error">
                    {t('scenarioRunner.unusable', { problem: scenario.problem })}
                  </p>
                )}
                <p className="message-note" hidden={scenario.problem !== null}>
                  {scenario.isCohort
                    ? t('scenarioRunner.cohort')
                    : `${t('scenarioRunner.patients', { count: scenario.patientCount })} · ${t(
                        'scenarioRunner.pathway',
                        { pathway: scenario.pathwayId },
                      )}`}
                </p>
              </div>
              <button
                type="button"
                disabled={busy || scenario.isCohort || scenario.problem !== null}
                onClick={() => void start(scenario.name)}
              >
                {t('scenarioRunner.run', { name: scenario.name })}
              </button>
            </li>
          ))}
        </ul>
      </article>

      {run && (
        <RunPanel
          key={run.runId}
          runId={run.runId}
          initial={run}
          {...(pollIntervalMs === undefined ? {} : { pollIntervalMs })}
        />
      )}
    </section>
  );
}
