import { useTranslation } from 'react-i18next';
import { Route, Routes } from 'react-router-dom';

import { NavBar, SimulatedBanner } from './components';
import { PatientBehavioursPage } from './features/patient-behaviours';
import { InboxPage } from './features/inbox';
import { MessageLogPage } from './features/message-log';
import { ScenarioRunnerPage } from './features/scenario-runner';
import { ROUTES } from './routes';

/** How often the inbox and a scenario run are re-read, so reactions appear without a refresh. */
const POLL_INTERVAL_MS = 2000;

/**
 * The messaging simulator shell: simulated-data banner, navigation and one route per screen.
 *
 * @remarks
 * Must be rendered inside a router (`BrowserRouter` in `main.tsx`, `MemoryRouter` in tests).
 * The banner is always visible because everything this UI shows is synthetic.
 */
export function App() {
  const { t } = useTranslation();

  return (
    <>
      <SimulatedBanner />
      <header className="app-header">
        <h1>{t('app.title')}</h1>
        <NavBar />
      </header>
      <main className="app-main">
        <Routes>
          <Route path={ROUTES.inbox} element={<InboxPage pollIntervalMs={POLL_INTERVAL_MS} />} />
          <Route path={ROUTES.patientBehaviours} element={<PatientBehavioursPage />} />
          <Route
            path={ROUTES.scenarioRunner}
            element={<ScenarioRunnerPage pollIntervalMs={POLL_INTERVAL_MS} />}
          />
          <Route path={ROUTES.messageLog} element={<MessageLogPage />} />
        </Routes>
      </main>
    </>
  );
}
