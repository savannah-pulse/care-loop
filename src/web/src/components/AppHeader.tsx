import { useEscalationCount } from '../data'
import { SignedInMenu, isEscalationRole, useSignedInStaff } from '../identity'
import { t, tp } from '../i18n'
import { routeHref, type Route } from '../routing'
import { Logo } from './Icon'

type Section = 'worklist' | 'escalations' | 'impact' | 'programmes'

function sectionOf(route: Route): Section | undefined {
  switch (route.name) {
    case 'worklist':
    case 'case':
    case 'summary':
      return 'worklist'
    case 'escalations':
    case 'escalation':
      return 'escalations'
    case 'impact':
      return 'impact'
    case 'programmes':
      return 'programmes'
    case 'notFound':
      return undefined
  }
}

function EscalationCount() {
  const read = useEscalationCount()
  if (read.state.status !== 'ready' || read.state.data === 0) {
    return null
  }
  const count = read.state.data
  return (
    <span className="count">
      <span aria-hidden="true">{count}</span>
      <span className="visually-hidden">{tp('nav.escalationCount', count)}</span>
    </span>
  )
}

/** Props for {@link AppHeader}. */
export interface AppHeaderProps {
  /** The screen being shown, to mark its section in the navigation. */
  readonly route: Route
}

/**
 * The bar at the top of every screen: the CareLoop name, the sections and the "Signed in as" menu.
 *
 * @remarks
 * A case and its handoff summary belong to the worklist section, so Worklist stays marked there.
 * The facility lead and the programme coordinator, who are on the escalation ladder, also get
 * Escalations, with the number of open escalations; a case's escalation view belongs to it.
 */
export function AppHeader({ route }: AppHeaderProps) {
  const me = useSignedInStaff()
  const current = sectionOf(route)
  const sections: readonly Section[] = isEscalationRole(me.role)
    ? ['worklist', 'escalations', 'impact', 'programmes']
    : ['worklist', 'impact', 'programmes']
  return (
    <header className="appbar">
      <div className="appbar-inner">
        <a className="brand" href={routeHref({ name: 'worklist' })}>
          <Logo />
          <span className="brand-text">{t('app.name')}</span>
        </a>
        <nav className="nav" aria-label={t('nav.label')}>
          {sections.map((section) => (
            <a
              key={section}
              href={routeHref({ name: section })}
              className={section === current ? 'on' : undefined}
              aria-current={section === current ? 'page' : undefined}
            >
              {t(`nav.${section}`)}
              {section === 'escalations' ? <EscalationCount key={me.id} /> : null}
            </a>
          ))}
        </nav>
        <div className="grow" />
        <SignedInMenu />
      </div>
    </header>
  )
}
