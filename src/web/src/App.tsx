import { useEffect, useRef, type MouseEvent } from 'react'
import { AppHeader, ErrorBoundary } from './components'
import { CasePage } from './features/case'
import { ImpactPage } from './features/impact'
import { NotFoundPage } from './features/not-found'
import { ProgrammesPage } from './features/programmes'
import { SummaryPage } from './features/summary'
import { WorklistPage } from './features/worklist'
import { t } from './i18n'
import { useSignedInStaff } from './identity'
import { routeHref, useRoute, type Route } from './routing'

function Screen({ route }: { readonly route: Route }) {
  switch (route.name) {
    case 'worklist':
      return <WorklistPage />
    case 'case':
      return <CasePage key={route.caseId} caseId={route.caseId} />
    case 'summary':
      return <SummaryPage key={route.caseId} caseId={route.caseId} />
    case 'impact':
      return <ImpactPage />
    case 'programmes':
      return <ProgrammesPage />
    case 'notFound':
      return <NotFoundPage />
  }
}

/**
 * The CareLoop dashboard: the header and whichever screen the address points to.
 *
 * @remarks
 * On every change of screen, focus moves to the main content and the page scrolls to the top, so
 * keyboard and screen-reader users land on the new screen. The skip link moves focus without
 * changing the address, which the router owns. Each screen sits in an error boundary keyed by the
 * address and the signed-in person, so a screen that fails says so and the next one starts afresh.
 */
export function App() {
  const route = useRoute()
  const me = useSignedInStaff()
  const mainRef = useRef<HTMLElement>(null)
  const shownRoute = useRef(route)

  useEffect(() => {
    if (shownRoute.current === route) {
      return
    }
    shownRoute.current = route
    window.scrollTo(0, 0)
    mainRef.current?.focus()
  }, [route])

  const skipToContent = (event: MouseEvent<HTMLAnchorElement>): void => {
    event.preventDefault()
    mainRef.current?.focus()
  }

  return (
    <>
      <a className="skip-link" href="#main" onClick={skipToContent}>
        {t('app.skipToContent')}
      </a>
      <AppHeader route={route} />
      <main id="main" ref={mainRef} tabIndex={-1}>
        <ErrorBoundary key={`${me.id}${route.name === 'notFound' ? '' : routeHref(route)}`}>
          <Screen route={route} />
        </ErrorBoundary>
      </main>
    </>
  )
}
