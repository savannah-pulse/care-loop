import { SignedInMenu } from '../identity'
import { t } from '../i18n'
import { routeHref, type Route } from '../routing'
import { Logo } from './Icon'

type Section = 'worklist' | 'impact' | 'programmes'

const SECTIONS: readonly Section[] = ['worklist', 'impact', 'programmes']

function sectionOf(route: Route): Section | undefined {
  switch (route.name) {
    case 'worklist':
    case 'case':
    case 'summary':
      return 'worklist'
    case 'impact':
      return 'impact'
    case 'programmes':
      return 'programmes'
    case 'notFound':
      return undefined
  }
}

/** Props for {@link AppHeader}. */
export interface AppHeaderProps {
  /** The screen being shown, to mark its section in the navigation. */
  readonly route: Route
}

/**
 * The bar at the top of every screen: the CareLoop name, the three sections and the "Signed in as"
 * menu.
 *
 * @remarks
 * A case and its handoff summary belong to the worklist section, so Worklist stays marked there.
 */
export function AppHeader({ route }: AppHeaderProps) {
  const current = sectionOf(route)
  return (
    <header className="appbar">
      <div className="appbar-inner">
        <a className="brand" href={routeHref({ name: 'worklist' })}>
          <Logo />
          <span className="brand-text">{t('app.name')}</span>
        </a>
        <nav className="nav" aria-label={t('nav.label')}>
          {SECTIONS.map((section) => (
            <a
              key={section}
              href={routeHref({ name: section })}
              className={section === current ? 'on' : undefined}
              aria-current={section === current ? 'page' : undefined}
            >
              {t(`nav.${section}`)}
            </a>
          ))}
        </nav>
        <div className="grow" />
        <SignedInMenu />
      </div>
    </header>
  )
}
