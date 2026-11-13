import { PlaceholderPanel } from '../../components';

/** Props for {@link ScenarioRunnerPage}. */
export interface ScenarioRunnerPageProps {
  /**
   * Base URL of the HIS simulator API. Omit it to call the same origin that serves the ClientApp
   * (the Vite dev server proxies those calls to the simulator).
   */
  apiBaseUrl?: string;
}

/** Translation keys of the behaviours planned for this screen. */
const PLANNED_KEYS = [
  'scenarioRunner.planned.list',
  'scenarioRunner.planned.run',
  'scenarioRunner.planned.progress',
  'scenarioRunner.planned.expect',
] as const;

/**
 * The scenario runner screen: lists the scripted scenarios and replays one against CareLoop.
 *
 * @remarks
 * A run performs the scenario's steps, which are all hospital-system events. How the patient reacts to a notification is not a step: the patient-channel simulator applies the behaviour declared on the scenario patient. A step that CareLoop refuses (for example closure without evidence) is an expected, demonstrable result and is shown as rejected, not as a failure.
 */
export function ScenarioRunnerPage(_props: ScenarioRunnerPageProps) {
  // TODO(WS-7): list scenarios (GET /scenarios), start a run (POST /scenarios/{name}/run) and poll GET /scenarios/runs/{runId}.
  return (
    <PlaceholderPanel
      titleKey="scenarioRunner.title"
      descriptionKey="scenarioRunner.description"
      plannedKeys={PLANNED_KEYS}
    />
  );
}
