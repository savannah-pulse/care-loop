import createClient, { type Middleware } from 'openapi-fetch'
import type { paths } from '../api/schema'
import { getSignedInStaff } from '../identity'

/**
 * Base URL of the CareLoop API.
 *
 * @remarks
 * Set `VITE_CARELOOP_API_URL` at build time to call an API elsewhere. By default the app calls
 * `/api` on its own origin, and whatever serves the app forwards it to the API: the Vite dev server
 * locally, nginx in the container.
 */
export const API_BASE_URL: string = import.meta.env.VITE_CARELOOP_API_URL || '/api'

/** Request header that tells the API which person is acting. */
export const USER_HEADER = 'X-Demo-User'

/** Request header that tells the API which role the person is acting in. */
export const ROLE_HEADER = 'X-Demo-Role'

/**
 * Adds the signed-in person's identity headers to every API request.
 *
 * @remarks
 * Prototype identity scheme, matching the API's `DemoRoles`: the person and role are stated in
 * request headers. Real sign-in replaces this before any deployment handles real patient data. The
 * person is read at request time, so a switch in the header applies to the next call.
 */
export const identityHeaders: Middleware = {
  onRequest({ request }) {
    const person = getSignedInStaff()
    request.headers.set(USER_HEADER, person.reference)
    request.headers.set(ROLE_HEADER, person.role)
    return request
  },
}

/**
 * Typed client for the CareLoop API, generated from `contracts/openapi/careloop.json`.
 *
 * @remarks
 * Paths, parameters, bodies and responses are checked against `src/api/schema.d.ts`; run
 * `npm run api:generate` after the API contract changes.
 */
export const api = createClient<paths>({ baseUrl: API_BASE_URL })

api.use(identityHeaders)
