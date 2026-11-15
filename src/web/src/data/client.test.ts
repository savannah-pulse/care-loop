import { afterEach, describe, expect, it, vi } from 'vitest'
import { signInAs } from '../identity'
import { caseDetail, caseSummary } from '../testing/builders'
import { createCareLoopClient } from './client'
import { acknowledgeEscalation, loadEscalatedCases, mapAtMost } from './escalations'
import { failureKindFor, type FailureKind } from './outcome'
import { PAGE_SIZE, loadCaseList, readCase, reasonParameter } from './reads'
import { approveHandoffSummary, draftHandoffSummary } from './summaries'

const BASE_URL = 'http://careloop.test/api'

type Handler = (request: Request) => Response | Promise<Response>

function fakeApi(handler: Handler = () => Response.json([])) {
  const requests: Request[] = []
  const fetch = vi.fn(async (request: Request) => {
    requests.push(request)
    return handler(request)
  })
  return { client: createCareLoopClient({ baseUrl: BASE_URL, fetch }), requests }
}

function pagedApi(total: number) {
  const rows = Array.from({ length: total }, (_, index) => caseSummary({ caseId: `case-${index}` }))
  return fakeApi((request) => {
    const query = new URL(request.url).searchParams
    const skip = Number(query.get('skip'))
    return Response.json(rows.slice(skip, skip + Number(query.get('take'))))
  })
}

function problem(status: number, body: object): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/problem+json' } })
}

afterEach(() => {
  signInAs('huda-saleh')
})

describe('who is acting', () => {
  it('states the person, role and facility on every request', async () => {
    const { client, requests } = fakeApi()
    await loadCaseList({}, { client })
    expect(requests[0].headers.get('X-Demo-User')).toBe('huda-saleh')
    expect(requests[0].headers.get('X-Demo-Role')).toBe('nurse-navigator')
    expect(requests[0].headers.get('X-Demo-Facility')).toBe('family-health-centre')
  })

  it('leaves the facility out for the programme coordinator, who works across facilities', async () => {
    signInAs('obaid-al-suwaidi')
    const { client, requests } = fakeApi()
    await loadCaseList({}, { client })
    expect(requests[0].headers.get('X-Demo-User')).toBe('obaid-al-suwaidi')
    expect(requests[0].headers.get('X-Demo-Role')).toBe('coordinator')
    expect(requests[0].headers.has('X-Demo-Facility')).toBe(false)
  })

  it('follows a change of person on the very next request', async () => {
    const { client, requests } = fakeApi(() => Response.json(caseDetail()))
    await readCase('case-1', undefined, undefined, client)
    signInAs('layla-haddad')
    await readCase('case-1', undefined, undefined, client)
    expect(requests[1].headers.get('X-Demo-User')).toBe('layla-haddad')
    expect(requests[1].headers.get('X-Demo-Role')).toBe('receiving-clinician')
    expect(requests[1].headers.get('X-Demo-Facility')).toBe('general-hospital')
  })
})

describe('reading outside the team', () => {
  it('encodes filters without dropping zero or false', async () => {
    const { client, requests } = fakeApi()
    await loadCaseList({ facilityId: 'general-hospital', reason: '  Covering clinic & ward ', escalated: false }, { client })
    const url = new URL(requests[0].url)
    expect(url.pathname).toBe('/api/cases')
    expect(url.search).toContain('reason=Covering%20clinic%20%26%20ward')
    expect(Object.fromEntries(url.searchParams)).toEqual({
      facilityId: 'general-hospital',
      reason: 'Covering clinic & ward',
      escalated: 'false',
      skip: '0',
      take: String(PAGE_SIZE),
    })
  })

  it('never sends a blank reason', async () => {
    const { client, requests } = fakeApi((request) =>
      new URL(request.url).pathname === '/api/cases' ? Response.json([]) : Response.json(caseDetail()),
    )
    await readCase('case-1', '   ', undefined, client)
    await loadCaseList({ reason: '\n' }, { client })
    expect(new URL(requests[0].url).searchParams.has('reason')).toBe(false)
    expect(new URL(requests[1].url).searchParams.has('reason')).toBe(false)
    expect(reasonParameter(undefined)).toBeUndefined()
  })

  it('sends the reason with a case read', async () => {
    const { client, requests } = fakeApi(() => Response.json(caseDetail()))
    await readCase('case/1', 'The patient called me', undefined, client)
    const url = new URL(requests[0].url)
    expect(url.pathname).toBe('/api/cases/case%2F1')
    expect(url.searchParams.get('reason')).toBe('The patient called me')
  })

  it('searches a patient with exactly the reference the API returned', async () => {
    const { client, requests } = fakeApi()
    await loadCaseList({ patientReference: 'p-100245891' }, { client })
    expect(new URL(requests[0].url).searchParams.get('patientReference')).toBe('p-100245891')
  })
})

describe('failures', () => {
  it.each<[number, FailureKind]>([
    [400, 'invalid'],
    [403, 'forbidden'],
    [404, 'notFound'],
    [409, 'conflict'],
    [501, 'notAvailable'],
    [503, 'unavailable'],
    [500, 'error'],
  ])('reports HTTP %i as a %s failure, never as a successful fallback', async (status, kind) => {
    const { client } = fakeApi(() => problem(status, { title: 'Refused by the API', status }))
    const outcome = await draftHandoffSummary('case-1', client)
    expect(outcome).toEqual({ ok: false, failure: { kind, status, detail: 'Refused by the API' } })
    expect(failureKindFor(status)).toBe(kind)
  })

  it('reads the message of a feature that is not built yet', async () => {
    const { client } = fakeApi(() =>
      problem(501, { workstream: 'WS9', feature: 'handoff-summary', message: 'Handoff summaries are not available yet.' }),
    )
    const outcome = await draftHandoffSummary('case-1', client)
    expect(outcome).toEqual({
      ok: false,
      failure: { kind: 'notAvailable', status: 501, detail: 'Handoff summaries are not available yet.' },
    })
  })

  it('reports an empty error body without inventing an explanation', async () => {
    const { client } = fakeApi(() => new Response(null, { status: 400 }))
    const outcome = await readCase('case-1', undefined, undefined, client)
    expect(outcome).toEqual({ ok: false, failure: { kind: 'invalid', status: 400, detail: undefined } })
  })

  it('reports a network failure as an error', async () => {
    const { client } = fakeApi(() => Promise.reject(new TypeError('Failed to fetch')))
    expect(await readCase('case-1', undefined, undefined, client)).toEqual({ ok: false, failure: { kind: 'error' } })
  })

  it('keeps a cancelled read apart from a failure', async () => {
    const { client } = fakeApi(
      (request) =>
        new Promise<Response>((_, reject) => {
          const cancel = (): void => reject(new DOMException('The read was cancelled.', 'AbortError'))
          if (request.signal.aborted) {
            cancel()
          } else {
            request.signal.addEventListener('abort', cancel)
          }
        }),
    )
    const controller = new AbortController()
    const pending = readCase('case-1', undefined, controller.signal, client)
    controller.abort()
    expect(await pending).toEqual({ ok: false, failure: { kind: 'aborted' } })
  })
})

describe('handoff summary approval', () => {
  it('approves exactly the edited text, including whitespace', async () => {
    const { client, requests } = fakeApi(() => Response.json({}))
    const text = '  Human-reviewed facts.\n\nNext action unchanged.  '
    await approveHandoffSummary('case-1', 'draft-1', text, client)
    expect(requests[0].method).toBe('POST')
    expect(new URL(requests[0].url).pathname).toBe('/api/cases/case-1/handoff-summary/draft-1/approval')
    expect(await requests[0].text()).toBe(JSON.stringify({ approvedText: text }))
  })
})

describe('escalation acknowledgement', () => {
  it('acknowledges exactly one escalation without sending any change to the case', async () => {
    const { client, requests } = fakeApi(() => Response.json(caseDetail()))
    const outcome = await acknowledgeEscalation('case-1', 'notice/2', ' Covering clinic ', client)
    const url = new URL(requests[0].url)
    expect(requests[0].method).toBe('POST')
    expect(url.pathname).toBe('/api/cases/case-1/escalations/notice%2F2/acknowledgement')
    expect(url.searchParams.get('reason')).toBe('Covering clinic')
    expect(await requests[0].text()).toBe('')
    expect(outcome.ok).toBe(true)
  })

  it.each<[number, FailureKind]>([
    [403, 'forbidden'],
    [409, 'conflict'],
  ])('reports a refused acknowledgement (HTTP %i) as %s', async (status, kind) => {
    const { client } = fakeApi(() => problem(status, { title: 'Not acknowledged', status }))
    const outcome = await acknowledgeEscalation('case-1', 'escalation-1', undefined, client)
    expect(outcome).toMatchObject({ ok: false, failure: { kind, status } })
  })
})

describe('paging', () => {
  it('reads every page of at most 200 cases until a short page', async () => {
    const { client, requests } = pagedApi(450)
    const outcome = await loadCaseList({}, { client })
    expect(requests.map((request) => new URL(request.url).search)).toEqual([
      '?skip=0&take=200',
      '?skip=200&take=200',
      '?skip=400&take=200',
    ])
    expect(outcome).toMatchObject({ ok: true, data: { complete: true } })
    expect(outcome.ok && outcome.data.cases).toHaveLength(450)
  })

  it('stops at the limit and says the list may be incomplete', async () => {
    const { client, requests } = pagedApi(450)
    const outcome = await loadCaseList({}, { client, limit: 300 })
    expect(requests.map((request) => new URL(request.url).searchParams.get('take'))).toEqual(['200', '100'])
    expect(outcome).toMatchObject({ ok: true, data: { complete: false } })
    expect(outcome.ok && outcome.data.cases).toHaveLength(300)
  })

  it('reads a list that ends exactly on a page boundary as complete', async () => {
    const { client, requests } = pagedApi(200)
    const outcome = await loadCaseList({}, { client })
    expect(requests).toHaveLength(2)
    expect(outcome).toMatchObject({ ok: true, data: { complete: true } })
  })

  it('lists a case that moved between pages once', async () => {
    const first = Array.from({ length: PAGE_SIZE }, (_, index) => caseSummary({ caseId: `case-${index}` }))
    let call = 0
    const { client } = fakeApi(() => {
      call += 1
      return Response.json(call === 1 ? first : [first[PAGE_SIZE - 1], caseSummary({ caseId: 'case-last' })])
    })
    const outcome = await loadCaseList({}, { client })
    const ids = outcome.ok ? outcome.data.cases.map((row) => row.caseId) : []
    expect(ids).toHaveLength(PAGE_SIZE + 1)
    expect(new Set(ids).size).toBe(ids.length)
    expect(ids.at(-1)).toBe('case-last')
  })

  it('reports a failed page instead of a partial list', async () => {
    const first = Array.from({ length: PAGE_SIZE }, (_, index) => caseSummary({ caseId: `case-${index}` }))
    let call = 0
    const { client } = fakeApi(() => {
      call += 1
      return call === 1 ? Response.json(first) : problem(403, { title: 'Forbidden' })
    })
    expect(await loadCaseList({}, { client })).toMatchObject({ ok: false, failure: { kind: 'forbidden', status: 403 } })
  })
})

describe('escalated cases', () => {
  it('reads each escalated case in full and keeps the list row when a case cannot be read', async () => {
    const { client, requests } = fakeApi((request) => {
      const url = new URL(request.url)
      if (url.pathname === '/api/cases') {
        return Response.json([caseSummary({ caseId: 'a', isEscalated: true }), caseSummary({ caseId: 'b', isEscalated: true })])
      }
      return url.pathname.endsWith('/a') ? Response.json(caseDetail({ caseId: 'a' })) : problem(403, { title: 'Forbidden' })
    })
    const outcome = await loadEscalatedCases(undefined, client)
    expect(new URL(requests[0].url).searchParams.get('escalated')).toBe('true')
    expect(outcome.ok && outcome.data.map((row) => [row.summary.caseId, row.detail?.caseId])).toEqual([
      ['a', 'a'],
      ['b', undefined],
    ])
  })

  it('runs a few reads at a time and keeps their order', async () => {
    let running = 0
    let most = 0
    const results = await mapAtMost([30, 10, 20, 0, 5], 2, async (wait) => {
      running += 1
      most = Math.max(most, running)
      await new Promise((resolve) => setTimeout(resolve, wait))
      running -= 1
      return wait * 2
    })
    expect(results).toEqual([60, 20, 40, 0, 10])
    expect(most).toBe(2)
  })
})
