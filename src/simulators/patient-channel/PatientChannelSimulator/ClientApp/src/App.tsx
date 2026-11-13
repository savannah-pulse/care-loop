import { useTranslation } from 'react-i18next';
import { Route, Routes } from 'react-router-dom';

import { NavBar, SimulatedBanner } from './components';
import { CaseStatusPage } from './features/case-status';
import { PatientBehavioursPage } from './features/patient-behaviours';
import { InboxPage } from './features/inbox';
import { MessageLogPage } from './features/message-log';
import { ScenarioRunnerPage } from './features/scenario-runner';
import { ROUTES } from './routes';

/**
 * The patient-channel simulator shell: simulated-data banner, navigation and one route per screen.
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
          <Route path={ROUTES.inbox} element={<InboxPage />} />
          <Route path={ROUTES.caseStatus} element={<CaseStatusPage />} />
          <Route path={ROUTES.patientBehaviours} element={<PatientBehavioursPage />} />
          <Route path={ROUTES.scenarioRunner} element={<ScenarioRunnerPage />} />
          <Route path={ROUTES.messageLog} element={<MessageLogPage />} />
        </Routes>
      </main>
    </>
  );
}
