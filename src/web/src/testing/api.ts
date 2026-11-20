/**
 * A fake CareLoop API for the screen tests, answering at the `fetch` level.
 *
 * @remarks
 * {@link installFakeApi} puts it in place of the network, so screens run with the dashboard's own
 * client, identity headers and reads, and no request leaves the test. A test says how each route
 * answers with {@link answer}, using the synthetic responses in `builders.ts`, and checks what the
 * screens sent with {@link sentRequests}. A request the test did not answer gets an HTTP 500 and is
 * reported after the test by {@link takeUnanswered}, so a screen never reads more than a test
 * expects without anyone noticing.
 */

import { vi } from 'vitest'

/** Where the dashboard calls the API on its own origin; routes are written without it. */
const API_PREFIX = '/api'

/** A request a screen sent, as the fake API received it. */
export interface SentRequest {
  /** The method and the path after `/api`, without the query: for example `GET /cases/case-1`. */
  readonly route: string
  /** The query parameters. */
  readonly query: URLSearchParams
  /** The request headers, including the identity headers. */
  readonly headers: Headers
  /** The body exactly as it was sent, or an empty string when there was none. */
  readonly body: string
}

/**
 * How the fake API answers a route: a response, copied for each request, or a function that builds
 * the response from the request.
 */
export type Answer = Response | ((request: SentRequest) => Response | Promise<Response>)

const answers = new Map<string, Answer>()
const received: SentRequest[] = []
const unanswered: string[] = []
let generation = 0

/**
 * Sets how the fake API answers a route from now on, replacing any earlier answer for it.
 *
 * @param route - The method and the path after `/api`, without the query: for example
 * `GET /cases/case-1` or `POST /cases/case-1/call-outcome`.
 * @param response - The answer.
 */
export function answer(route: string, response: Answer): void {
  answers.set(route, response)
}

/**
 * Builds a JSON answer.
 *
 * @param body - The body, sent as JSON.
 * @param status - The HTTP status; 200 by default.
 * @returns The response.
 */
export function json(body: unknown, status: number = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

/**
 * Builds a refusal or failure in the API's problem format (RFC 9457).
 *
 * @param status - The HTTP status, for example 403, 409 or 501.
 * @param title - What the API says went wrong.
 * @returns The response.
 */
export function problem(status: number, title: string = 'Refused by the API'): Response {
  return new Response(JSON.stringify({ title, status }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

/**
 * Lists the requests the screens sent, oldest first.
 *
 * @param route - Only requests to this route, for example `POST /cases/case-1/call-outcome`;
 * every request when absent.
 * @returns The requests.
 */
export function sentRequests(route?: string): SentRequest[] {
  return received.filter((request) => route === undefined || request.route === route)
}

/**
 * Takes the routes the screens called that the test did not answer.
 *
 * @returns The routes, once per request; the list is then empty.
 */
export function takeUnanswered(): string[] {
  return unanswered.splice(0)
}

/**
 * Forgets every answer and every request, ready for the next test.
 *
 * @remarks
 * A request still on its way from the test that ended is dropped, so it can never be counted, or
 * reported as unanswered, in the next test.
 */
export function resetFakeApi(): void {
  generation += 1
  answers.clear()
  received.length = 0
  unanswered.length = 0
}

function cancelled(): DOMException {
  return new DOMException('The request was cancelled.', 'AbortError')
}

async function fakeFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const request = input instanceof Request ? input : new Request(input, init)
  if (request.signal.aborted) {
    // As in a browser, a request cancelled before it was sent never reaches the API.
    throw cancelled()
  }
  const sentIn = generation
  const url = new URL(request.url)
  const path = url.pathname.startsWith(API_PREFIX) ? url.pathname.slice(API_PREFIX.length) : url.pathname
  const body = await request.text()
  if (sentIn !== generation) {
    throw cancelled()
  }
  const sent: SentRequest = { route: `${request.method} ${path}`, query: url.searchParams, headers: request.headers, body }
  received.push(sent)
  if (request.signal.aborted) {
    throw cancelled()
  }
  const reply = answers.get(sent.route)
  if (reply === undefined) {
    unanswered.push(sent.route)
    return problem(500, `The test has no answer for ${sent.route}.`)
  }
  return typeof reply === 'function' ? reply(sent) : reply.clone()
}

/**
 * Puts the fake API in place of the network.
 *
 * @remarks
 * The test setup calls it once per test file, before anything imports the dashboard's API client,
 * which keeps the `fetch` and `Request` it finds when it is created. `Request` also learns to take
 * an address relative to the page, as it does in a browser, because the client calls `/api` on the
 * dashboard's own origin and Node's `Request` needs a whole address.
 */
export function installFakeApi(): void {
  const BaseRequest = globalThis.Request
  class PageRequest extends BaseRequest {
    constructor(input: RequestInfo | URL, init?: RequestInit) {
      super(typeof input === 'string' ? new URL(input, document.baseURI).href : input, init)
    }
  }
  vi.stubGlobal('Request', PageRequest)
  vi.stubGlobal('fetch', fakeFetch)
}
