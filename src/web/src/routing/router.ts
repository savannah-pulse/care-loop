import { useMemo, useSyncExternalStore } from 'react'

/**
 * A screen of the app, with the values its address carries.
 *
 * @remarks
 * The app routes on the URL hash (`#/cases/{id}`), so any static host can serve it and a reload
 * never reaches the API by mistake.
 */
export type Route =
  | { readonly name: 'worklist' }
  | { readonly name: 'case'; readonly caseId: string }
  | { readonly name: 'escalation'; readonly caseId: string }
  | { readonly name: 'escalations' }
  | { readonly name: 'impact' }
  | { readonly name: 'programmes'; readonly pathwayId?: string }
  | { readonly name: 'notFound' }

/** A route that has an address of its own; every route except "not found". */
export type AddressableRoute = Exclude<Route, { readonly name: 'notFound' }>

function decodeSegments(path: string): string[] | undefined {
  try {
    return path
      .split('/')
      .filter((segment) => segment !== '')
      .map((segment) => decodeURIComponent(segment))
  } catch {
    return undefined
  }
}

/**
 * Works out which screen an address points to.
 *
 * @param hash - The URL hash, for example `#/cases/3f6b1c2a-8d4e-4f5a-9b7c-100245891000/escalation`.
 * @returns The matching route, or `notFound` when nothing matches.
 *
 * @example
 * ```ts
 * parseRoute('#/impact') // { name: 'impact' }
 * parseRoute('#/programmes/breast-screening') // { name: 'programmes', pathwayId: 'breast-screening' }
 * parseRoute('') // { name: 'worklist' }
 * ```
 */
export function parseRoute(hash: string): Route {
  const segments = decodeSegments(hash.replace(/^#/, ''))
  if (!segments) {
    return { name: 'notFound' }
  }
  const [first, second, third, ...rest] = segments
  if (rest.length > 0) {
    return { name: 'notFound' }
  }
  if (first === undefined) {
    return { name: 'worklist' }
  }
  if (first === 'cases' && second !== undefined) {
    if (third === undefined) {
      return { name: 'case', caseId: second }
    }
    if (third === 'escalation') {
      return { name: 'escalation', caseId: second }
    }
    return { name: 'notFound' }
  }
  if (first === 'programmes' && third === undefined) {
    return second === undefined ? { name: 'programmes' } : { name: 'programmes', pathwayId: second }
  }
  if (second !== undefined) {
    return { name: 'notFound' }
  }
  if (first === 'impact') {
    return { name: 'impact' }
  }
  if (first === 'escalations') {
    return { name: 'escalations' }
  }
  return { name: 'notFound' }
}

/**
 * Builds the address of a screen, for links.
 *
 * @param route - The screen to link to.
 * @returns The hash address, for example `#/cases/{caseId}`.
 */
export function routeHref(route: AddressableRoute): string {
  switch (route.name) {
    case 'worklist':
      return '#/'
    case 'case':
      return `#/cases/${encodeURIComponent(route.caseId)}`
    case 'escalation':
      return `#/cases/${encodeURIComponent(route.caseId)}/escalation`
    case 'escalations':
      return '#/escalations'
    case 'impact':
      return '#/impact'
    case 'programmes':
      return route.pathwayId ? `#/programmes/${encodeURIComponent(route.pathwayId)}` : '#/programmes'
  }
}

/**
 * Opens a screen by changing the URL hash, which adds a browser history entry.
 *
 * @param route - The screen to open.
 */
export function navigate(route: AddressableRoute): void {
  window.location.hash = routeHref(route)
}

function subscribe(onChange: () => void): () => void {
  window.addEventListener('hashchange', onChange)
  return () => window.removeEventListener('hashchange', onChange)
}

function readHash(): string {
  return window.location.hash
}

/**
 * Follows the current screen.
 *
 * @returns The route for the current URL hash.
 *
 * @remarks
 * Subscribes to `hashchange`, so the component re-renders when the person follows a link or uses
 * the browser's back and forward buttons.
 */
export function useRoute(): Route {
  const hash = useSyncExternalStore(subscribe, readHash)
  return useMemo(() => parseRoute(hash), [hash])
}
