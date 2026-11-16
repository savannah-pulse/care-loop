import createClient, { type Client, type Middleware } from 'openapi-fetch'
import type { paths } from '../api/schema'
import { getSignedInStaff } from '../identity'

/**
 * Where the dashboard calls the CareLoop API: `/api` on its own origin.
 *
 * @remarks
 * Whatever serves the app forwards `/api` to the API without the prefix: the Vite dev server
 * locally (to `CARELOOP_API_URL`), nginx in the container. The app never calls another origin.
 */
export const API_BASE_URL = '/api'

/** Request header that tells the API which person is acting. */
export const USER_HEADER = 'X-Demo-User'

/** Request header that tells the API which role the person is acting in. */
export const ROLE_HEADER = 'X-Demo-Role'

/**
 * Request header that tells the API which facility the person works at: their team.
 *
 * @remarks
 * Staff reads default to this facility. The programme coordinator works across facilities, so the
 * header is left out for them.
 */
export const FACILITY_HEADER = 'X-Demo-Facility'

/**
 * Adds the signed-in person's identity headers to every API request.
 *
 * @remarks
 * Prototype identity scheme, matching the API's `DemoRoles`: the person, role and facility are
 * stated in request headers. Real sign-in replaces this before any deployment handles real patient
 * data. The person is read at request time, so a switch in the header applies to the next call.
 */
export const identityHeaders: Middleware = {
  onRequest({ request }) {
    const person = getSignedInStaff()
    request.headers.set(USER_HEADER, person.reference)
    request.headers.set(ROLE_HEADER, person.role)
    if (person.facilityId) {
      request.headers.set(FACILITY_HEADER, person.facilityId)
    } else {
      request.headers.delete(FACILITY_HEADER)
    }
    return request
  },
}

/** A typed client for the CareLoop API, generated from `contracts/openapi/careloop.json`. */
export type CareLoopClient = Client<paths>

/** Settings for {@link createCareLoopClient}. */
export interface CareLoopClientOptions {
  /** Where the API is; {@link API_BASE_URL} by default. */
  readonly baseUrl?: string
  /** The `fetch` to call the API with; the browser's by default. Tests pass their own. */
  readonly fetch?: (request: Request) => Promise<Response>
}

/**
 * Creates a typed CareLoop API client that states who is acting on every request.
 *
 * @param options - Where the API is and how to reach it.
 * @returns A client whose paths, parameters, bodies and responses are checked against
 * `src/api/schema.d.ts`, with {@link identityHeaders} added.
 */
export function createCareLoopClient(options: CareLoopClientOptions = {}): CareLoopClient {
  const client = createClient<paths>({ baseUrl: options.baseUrl ?? API_BASE_URL, fetch: options.fetch })
  client.use(identityHeaders)
  return client
}

/**
 * The dashboard's CareLoop API client.
 *
 * @remarks
 * Run `npm run generate:api` after the API contract changes, so the types follow it.
 */
export const api: CareLoopClient = createCareLoopClient()
