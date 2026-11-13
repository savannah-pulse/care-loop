/**
 * A local stand-in for the CareLoop API, for building the dashboard before the API is ready.
 *
 * Registered by `vite.config.ts` only when `CARELOOP_MOCK_API=1`, for both `vite` (dev) and
 * `vite preview`. It answers the same paths as the API under `/api` from the fixtures in
 * `fixtures.ts`, keeps drafted, approved and rejected summaries in memory until the server stops,
 * and checks the identity headers the way the API's prototype identity scheme does. It is never part
 * of the built app: the app knows nothing about it.
 */

import { randomUUID } from 'node:crypto'
import type { IncomingMessage, ServerResponse } from 'node:http'
import type { Connect, Plugin } from 'vite'
import type { components } from '../src/api/schema.d.ts'
import {
  COHORT_SEED,
  HESSA_CASE_ID,
  WITHOUT_ESCALATION,
  WITH_ESCALATION,
  createCases,
  createMetrics,
  draftHessaSummary,
  mockNow,
  startScenarioClock,
  type CaseFixture,
  type HandoffSummary,
} from './fixtures.ts'

type Schemas = components['schemas']
type Problem = Schemas['ProblemDetails']

/** Roles that may approve or reject a handoff summary in the mock, like the clinician roles in the app. */
const SUMMARY_REVIEWER_ROLES: readonly string[] = ['clinician', 'receiving-clinician']

interface MockState {
  readonly cases: CaseFixture[]
  readonly summaries: Map<string, HandoffSummary>
}

class HttpError extends Error {
  readonly status: number

  constructor(status: number, title: string) {
    super(title)
    this.status = status
  }
}

function send(res: ServerResponse, status: number, body?: unknown): void {
  res.statusCode = status
  if (body === undefined) {
    res.end()
    return
  }
  res.setHeader('Content-Type', status >= 400 ? 'application/problem+json' : 'application/json; charset=utf-8')
  res.end(JSON.stringify(body))
}

function problem(status: number, title: string, detail?: string): Problem {
  return { type: null, title, status, detail: detail ?? null, instance: null }
}

async function readJson(req: IncomingMessage): Promise<Record<string, unknown>> {
  const chunks: Buffer[] = []
  for await (const chunk of req) {
    chunks.push(typeof chunk === 'string' ? Buffer.from(chunk) : (chunk as Buffer))
  }
  const text = Buffer.concat(chunks).toString('utf8')
  try {
    const value: unknown = text === '' ? {} : JSON.parse(text)
    if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
      return value as Record<string, unknown>
    }
  } catch {
    // Reported below as a bad request.
  }
  throw new HttpError(400, 'The request body is not a JSON object.')
}

function header(req: IncomingMessage, name: string): string | undefined {
  const value = req.headers[name.toLowerCase()]
  return Array.isArray(value) ? value[0] : value
}

function findCase(state: MockState, caseId: string): CaseFixture {
  const found = state.cases.find((fixture) => fixture.detail.caseId === caseId)
  if (!found) {
    throw new HttpError(404, 'Case not found.')
  }
  return found
}

function timeSpan(milliseconds: number): string {
  const totalSeconds = Math.floor(milliseconds / 1000)
  const days = Math.floor(totalSeconds / 86_400)
  const hours = Math.floor((totalSeconds % 86_400) / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60
  const clock = [hours, minutes, seconds].map((part) => String(part).padStart(2, '0')).join(':')
  return days > 0 ? `${days}.${clock}` : clock
}

function listCases(state: MockState, query: URLSearchParams): Schemas['CaseSummaryResponse'][] {
  const stage = query.get('stage')
  const owner = query.get('ownerReference')
  const escalated = query.get('escalated')
  const skip = Number(query.get('skip') ?? 0) || 0
  const take = Number(query.get('take') ?? 50) || 50
  return state.cases
    .map((fixture) => fixture.detail)
    .filter((detail) => !stage || detail.stage === stage)
    .filter((detail) => !owner || detail.owner.reference === owner)
    .filter((detail) => escalated === null || String(detail.isEscalated) === escalated)
    .sort((a, b) => Date.parse(b.updatedAt) - Date.parse(a.updatedAt))
    .slice(skip, skip + take)
    .map((detail) => ({
      caseId: detail.caseId,
      patientReference: detail.patientReference,
      pathwayId: detail.pathwayId,
      stage: detail.stage,
      urgency: detail.urgency,
      owner: detail.owner,
      nextAction: detail.nextAction,
      dueAt: detail.dueAt,
      isEscalated: detail.isEscalated,
      exceptions: [...detail.exceptions],
      updatedAt: detail.updatedAt,
    }))
}

function listExceptions(state: MockState, query: URLSearchParams): Schemas['ExceptionItemResponse'][] {
  const facility = query.get('facilityId')
  const owner = query.get('ownerReference')
  const now = mockNow().getTime()
  return state.cases
    .map((fixture) => fixture.detail)
    .filter((detail) => detail.exceptions.length > 0 && detail.nextAction && detail.dueAt)
    .filter((detail) => !facility || detail.facilityId === facility)
    .filter((detail) => !owner || detail.owner.reference === owner)
    .map((detail) => {
      const late = now - Date.parse(detail.dueAt ?? '')
      return {
        caseId: detail.caseId,
        patientReference: detail.patientReference,
        exceptions: [...detail.exceptions],
        stage: detail.stage,
        urgency: detail.urgency,
        nextAction: detail.nextAction ?? '',
        owner: detail.owner,
        dueAt: detail.dueAt ?? '',
        overdueBy: detail.exceptions.includes('Overdue') && late > 0 ? timeSpan(late) : null,
        isEscalated: detail.isEscalated,
        escalationLevel: detail.isEscalated ? 1 : 0,
      }
    })
}

function draftSummary(state: MockState, fixture: CaseFixture): HandoffSummary {
  const existing = [...state.summaries.values()].find(
    (summary) => summary.caseId === fixture.detail.caseId && summary.status === 'Approved',
  )
  if (existing) {
    throw new HttpError(409, 'This case already has an approved summary.')
  }
  if (fixture.detail.caseId !== HESSA_CASE_ID) {
    throw new HttpError(503, 'The AI service did not produce a draft that passed its checks.')
  }
  for (const [id, summary] of state.summaries) {
    if (summary.caseId === fixture.detail.caseId && summary.status === 'Draft') {
      state.summaries.delete(id)
    }
  }
  const draft = draftHessaSummary(randomUUID())
  state.summaries.set(draft.summaryId, draft)
  return draft
}

function openDraft(state: MockState, caseId: string, summaryId: string, req: IncomingMessage): HandoffSummary {
  const role = header(req, 'X-Demo-Role') ?? ''
  if (!SUMMARY_REVIEWER_ROLES.includes(role)) {
    throw new HttpError(403, '')
  }
  const summary = state.summaries.get(summaryId)
  if (!summary || summary.caseId !== caseId) {
    throw new HttpError(404, 'Summary not found.')
  }
  if (summary.status !== 'Draft') {
    throw new HttpError(409, 'This summary has already been decided.')
  }
  return summary
}

function approve(state: MockState, fixture: CaseFixture, summary: HandoffSummary, req: IncomingMessage, text: string): HandoffSummary {
  const now = mockNow().toISOString()
  const approver = header(req, 'X-Demo-User') ?? 'unknown'
  const approved: HandoffSummary = {
    ...summary,
    status: 'Approved',
    text,
    humanReviewRequired: false,
    approvedBy: approver,
    approvedAt: now,
  }
  state.summaries.set(summary.summaryId, approved)
  const detail = fixture.detail
  fixture.timeline.push({
    auditEventId: randomUUID(),
    eventType: 'HandoffSummaryApproved',
    disposition: 'Applied',
    actorReference: approver,
    actorRole: header(req, 'X-Demo-Role') ?? '',
    sourceTimestamp: now,
    receivedTimestamp: now,
    priorStage: detail.stage,
    newStage: detail.stage,
    reason: 'Handoff summary approved.',
    evidenceReference: `DocumentReference/${summary.summaryId}`,
  })
  fixture.detail = { ...detail, updatedAt: now }
  return approved
}

function reject(state: MockState, summary: HandoffSummary, req: IncomingMessage, reason: string): HandoffSummary {
  const rejected: HandoffSummary = {
    ...summary,
    status: 'Rejected',
    rejectedBy: header(req, 'X-Demo-User') ?? 'unknown',
    rejectedAt: mockNow().toISOString(),
    rejectionReason: reason,
  }
  state.summaries.set(summary.summaryId, rejected)
  return rejected
}

function requireText(body: Record<string, unknown>, field: string): string {
  const value = body[field]
  if (typeof value !== 'string' || value.trim() === '') {
    throw new HttpError(400, `The field "${field}" is required.`)
  }
  return value
}

async function route(state: MockState, req: IncomingMessage, res: ServerResponse): Promise<boolean> {
  const url = new URL(req.url ?? '/', 'http://careloop.local')
  const path = url.pathname.replace(/\/+$/, '') || '/'
  const method = req.method ?? 'GET'
  const segments = path.split('/').filter(Boolean).map(decodeURIComponent)
  const [first, caseId, third, summaryId, decision, ...rest] = segments

  const expect = (allowed: string): void => {
    if (method !== allowed) {
      throw new HttpError(405, `Use ${allowed} for this path.`)
    }
  }

  if (path === '/health') {
    expect('GET')
    send(res, 200, { status: 'ok', service: 'careloop-mock-api' })
    return true
  }
  if (path === '/cases') {
    expect('GET')
    send(res, 200, listCases(state, url.searchParams))
    return true
  }
  if (path === '/exceptions') {
    expect('GET')
    send(res, 200, listExceptions(state, url.searchParams))
    return true
  }
  if (path === '/metrics') {
    expect('GET')
    send(res, 200, createMetrics())
    return true
  }
  if (path === '/metrics/simulation') {
    expect('POST')
    const body = await readJson(req)
    const cohortScenario = requireText(body, 'cohortScenario')
    if (cohortScenario !== 'cohort') {
      throw new HttpError(400, `Unknown cohort scenario "${cohortScenario}".`)
    }
    const seed = body.seed === null || body.seed === undefined ? COHORT_SEED : Number(body.seed)
    send(res, 200, {
      cohortScenario,
      seed,
      withEscalation: WITH_ESCALATION,
      withoutEscalation: WITHOUT_ESCALATION,
    })
    return true
  }
  if (first !== 'cases' || caseId === undefined || rest.length > 0) {
    return false
  }
  const fixture = findCase(state, caseId)
  if (third === undefined) {
    expect('GET')
    send(res, 200, fixture.detail)
    return true
  }
  if (third === 'timeline' && summaryId === undefined) {
    expect('GET')
    send(res, 200, fixture.timeline)
    return true
  }
  if (third !== 'handoff-summary') {
    return false
  }
  if (summaryId === undefined) {
    expect('POST')
    send(res, 201, draftSummary(state, fixture))
    return true
  }
  if (decision === 'approval' || decision === 'rejection') {
    expect('POST')
    const body = await readJson(req)
    const summary = openDraft(state, caseId, summaryId, req)
    send(
      res,
      200,
      decision === 'approval'
        ? approve(state, fixture, summary, req, requireText(body, 'approvedText'))
        : reject(state, summary, req, requireText(body, 'reason').trim()),
    )
    return true
  }
  return false
}

/** Settings for {@link careLoopMockApi}. */
export interface MockApiOptions {
  /** The moment the fixtures describe, as an ISO 8601 date-time (`CARELOOP_MOCK_NOW`); now when absent. */
  readonly now?: string
}

/**
 * Creates the Vite plugin that answers `/api` from the fixtures.
 *
 * @param options - Where the fixtures' moment falls on the real clock.
 * @returns A plugin adding the mock API to the dev server and to `vite preview`.
 */
export function careLoopMockApi(options: MockApiOptions = {}): Plugin {
  startScenarioClock(options.now)
  const state: MockState = { cases: createCases(), summaries: new Map() }

  const handler: Connect.NextHandleFunction = (req, res) => {
    route(state, req, res)
      .then((handled) => {
        if (!handled) {
          send(res, 404, problem(404, 'No such path in the CareLoop API.'))
        }
      })
      .catch((error: unknown) => {
        if (error instanceof HttpError) {
          send(res, error.status, error.status === 403 ? undefined : problem(error.status, error.message))
          return
        }
        send(res, 500, problem(500, 'The mock API failed.', String(error)))
      })
  }

  return {
    name: 'careloop-mock-api',
    configureServer(server) {
      server.middlewares.use('/api', handler)
      server.config.logger.info(`  CareLoop mock API answers /api (scenario time ${mockNow().toISOString()})`)
    },
    configurePreviewServer(server) {
      server.middlewares.use('/api', handler)
      server.config.logger.info(`  CareLoop mock API answers /api (scenario time ${mockNow().toISOString()})`)
    },
  }
}
