import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Navigate, Route, Routes } from 'react-router-dom';

import { AppNav, SimulatedBanner } from './components';
import type { AppNavItem } from './components';
import { ClinicianActionsPage } from './features/clinician-actions';
import { FhirLogPage } from './features/fhir-log';
import { ScenarioRunnerPage } from './features/scenario-runner';
import { StaffInboxPage } from './features/staff-inbox';

/** The simulator's screens, in navigation order. */
const NAV_ITEMS: readonly AppNavItem[] = [
  { to: '/scenarios', labelKey: 'nav.scenarios' },
  { to: '/inbox', labelKey: 'nav.inbox' },
  { to: '/fhir-log', labelKey: 'nav.fhirLog' },
  { to: '/actions', labelKey: 'nav.actions' },
];

/**
 * The HIS simulator shell: simulated-data banner, navigation and the four screens.
 *
 * @remarks
 * Must be rendered inside a router. The entry point uses hash routing so client-side routes can
 * never collide with the simulator's API paths (`/scenarios`, `/fhir`, ...), which share the origin.
 */
export function App() {
  const { t } = useTranslation();

  useEffect(() => {
    document.title = t('app.title');
  }, [t]);

  return (
    <div className="app">
      <SimulatedBanner />
      <header className="app__header">
        <h1>{t('app.title')}</h1>
        <p>{t('app.subtitle')}</p>
      </header>
      <AppNav items={NAV_ITEMS} />
      <main className="app__main">
        <Routes>
          <Route path="/" element={<Navigate to="/scenarios" replace />} />
          <Route path="/scenarios" element={<ScenarioRunnerPage />} />
          <Route path="/inbox" element={<StaffInboxPage />} />
          <Route path="/fhir-log" element={<FhirLogPage />} />
          <Route path="/actions" element={<ClinicianActionsPage />} />
        </Routes>
      </main>
    </div>
  );
}
