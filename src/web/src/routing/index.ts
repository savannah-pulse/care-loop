/**
 * Routing: a small hash router for the screens (`#/`, `#/cases/{id}`, `#/cases/{id}/summary`,
 * `#/cases/{id}/escalation`, `#/escalations`, `#/impact` and `#/programmes/{pathwayId}`), with no
 * router dependency.
 */

export { navigate, parseRoute, routeHref, useRoute } from './router'
export type { AddressableRoute, Route } from './router'
