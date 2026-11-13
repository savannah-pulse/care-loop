import { useTranslation } from 'react-i18next';
import { NavLink } from 'react-router-dom';

/** One entry in the simulator's navigation. */
export interface AppNavItem {
  /** Client-side route the entry links to, for example `/scenarios`. */
  to: string;
  /** Key in `locales/en.json` for the entry's label. */
  labelKey: string;
}

/** Props for {@link AppNav}. */
export interface AppNavProps {
  /** The entries to show, in display order. */
  items: readonly AppNavItem[];
}

/**
 * The simulator's top-level navigation between its screens.
 *
 * @remarks
 * Used once in {@link App}. Labels come from the i18n layer and the layout uses logical CSS,
 * so the navigation works unchanged when a right-to-left language is added.
 */
export function AppNav({ items }: AppNavProps) {
  const { t } = useTranslation();

  return (
    <nav className="app-nav" aria-label={t('nav.label')}>
      <ul className="app-nav__list">
        {items.map((item) => (
          <li key={item.to}>
            <NavLink to={item.to} className="app-nav__link">
              {t(item.labelKey)}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
