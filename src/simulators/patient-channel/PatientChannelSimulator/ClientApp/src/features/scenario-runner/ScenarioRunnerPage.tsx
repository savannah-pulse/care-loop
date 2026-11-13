import { useTranslation } from 'react-i18next';

import { PlaceholderNotice } from '../../components';

/** i18n keys of the behaviours planned for this screen. */
const PLANNED_KEYS = [
  'scenarioRunner.planned.list',
  'scenarioRunner.planned.run',
  'scenarioRunner.planned.progress',
] as const;

/** Props for {@link ScenarioRunnerPage}. */
export interface ScenarioRunnerPageProps {
  /** How often to re-read a run in progress, in milliseconds. Polling is off when omitted. */
  pollIntervalMs?: number;
}

/**
 * Lists the shared scenarios, puts the patient behaviours of one in force and shows the reactions.
 *
 * @remarks
 * A run sends nothing by itself. It sets how the scenario's patients behave; they then react
 * only when CareLoop notifies them, which happens because the HIS simulator replays the same
 * scenario's hospital-system events. Placeholder until WS8.
 */
export function ScenarioRunnerPage(_props: ScenarioRunnerPageProps) {
  const { t } = useTranslation();

  // TODO(WS-8): list scenarios with listScenarios(), start a run with runScenario() and show progress from getScenarioRun().
  return (
    <section aria-labelledby="list-title">
      <h2 id="list-title">{t('scenarioRunner.title')}</h2>
      <p>{t('scenarioRunner.description')}</p>
      <PlaceholderNotice plannedKeys={PLANNED_KEYS} />
    </section>
  );
}
