import { useTranslation } from 'react-i18next';
import { NavLink } from 'react-router-dom';

import { ROUTES } from '../routes';

/** Navigation entries: route path and the i18n key of the link text, in display order. */
const NAV_ITEMS = [
  { to: ROUTES.inbox, labelKey: 'nav.inbox' },
  { to: ROUTES.caseStatus, labelKey: 'nav.caseStatus' },
  { to: ROUTES.patientBehaviours, labelKey: 'nav.patientBehaviours' },
  { to: ROUTES.scenarioRunner, labelKey: 'nav.scenarioRunner' },
  { to: ROUTES.messageLog, labelKey: 'nav.messageLog' },
] as const;

/**
 * Primary navigation between the simulator screens.
 *
 * @remarks
 * Rendered in the app header. Must be inside a router.
 */
export function NavBar() {
  const { t } = useTranslation();

  return (
    <nav className="app-nav" aria-label={t('nav.label')}>
      <ul>
        {NAV_ITEMS.map((item) => (
          <li key={item.to}>
            <NavLink to={item.to} end>
              {t(item.labelKey)}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
