/**
 * Routing: a small hash router for the five screens (`#/`, `#/cases/{id}`,
 * `#/cases/{id}/summary`, `#/impact` and `#/programmes`), with no router dependency.
 */

export { navigate, parseRoute, routeHref, useRoute } from './router'
export type { AddressableRoute, Route } from './router'
