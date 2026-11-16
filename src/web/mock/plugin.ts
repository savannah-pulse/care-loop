/**
 * A local stand-in for the CareLoop API, for building and checking the dashboard without it.
 *
 * Registered by `vite.config.ts` only when `CARELOOP_MOCK_API=1`, for both `vite` (dev) and
 * `vite preview`. It answers the same paths as the API under `/api` from the fixtures in
 * `fixtures.ts` and follows the API's read rules: the identity headers, the reader's facility as the
 * default scope, a recorded reason for anything outside it, the programme coordinator's view
 * (cases escalated to them only, without names), the clinician-only patient search, paging limits,
 * and who may acknowledge an escalation. Drafted, approved and rejected summaries and
 * acknowledgements last until the server stops. It is never part of the built app: the app knows
 * nothing about it.
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
  createProgrammes,
  draftHessaSummary,
  mockNow,
  startScenarioClock,
  type CaseFixture,
  type HandoffSummary,
  type Programme,
} from './fixtures.ts'

type Schemas = components['schemas']
type Problem = Schemas['ProblemDetails']
type CaseDetail = Schemas['CaseDetailResponse']
type CaseSummary = Schemas['CaseSummaryResponse']

/** Roles that may approve or reject a handoff summary in the mock, like the clinician roles in the app. */
const SUMMARY_REVIEWER_ROLES: readonly string[] = ['clinician', 'receiving-clinician']

/** Roles allowed to search a patient's cases, as in the API. */
const PATIENT_SEARCH_ROLES: readonly string[] = ['clinician', 'receiving-clinician']

/** Every role a person can hold, as in the API's `DemoRoles.HumanRoles`. */
const HUMAN_ROLES: readonly string[] = [
  'clinician',
  'coordinator',
  'facility-lead',
  'nurse-navigator',
  'receiving-clinician',
  'lab',
]

interface MockState {
  readonly cases: CaseFixture[]
  readonly summaries: Map<string, HandoffSummary>
  readonly programmes: Programme[]
}

interface Reader {
  readonly reference: string
  readonly role: string
  readonly facilityId?: string
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
  const text = (Array.isArray(value) ? value[0] : value)?.trim()
  return text ? text : undefined
}

function reader(req: IncomingMessage): Reader {
  const reference = header(req, 'X-Demo-User')
  const role = header(req, 'X-Demo-Role')?.toLowerCase()
  const facilityId = header(req, 'X-Demo-Facility')
  if (!reference || !role) {
    throw new HttpError(401, 'Staff identity headers are required.')
  }
  if (!HUMAN_ROLES.includes(role)) {
    throw new HttpError(403, 'This role may not read case data.')
  }
  if (role !== 'coordinator' && !facilityId) {
    throw new HttpError(400, 'A staff facility header is required.')
  }
  return { reference, role, facilityId: role === 'coordinator' ? undefined : facilityId }
}

function blank(value: string | null): boolean {
  return value === null || value.trim() === ''
}

function hasOpenCoordinatorEscalation(detail: CaseDetail): boolean {
  return detail.escalations.some((escalation) => !escalation.resolvedAt && escalation.escalatedTo.role === 'coordinator')
}

function checkOutsideTeam(who: Reader, facilityId: string | null, reason: string | null): void {
  if (who.role !== 'coordinator' && facilityId !== null && facilityId !== who.facilityId && blank(reason)) {
    throw new HttpError(403, 'Reading outside your facility requires a documented reason.')
  }
}

function checkCase(who: Reader, detail: CaseDetail, reason: string | null): void {
  if (who.role === 'coordinator') {
    if (!hasOpenCoordinatorEscalation(detail)) {
      throw new HttpError(403, 'A coordinator may read only cases escalated to that role.')
    }
    return
  }
  checkOutsideTeam(who, detail.facilityId, reason)
}

function findCase(state: MockState, caseId: string): CaseFixture {
  const found = state.cases.find((fixture) => fixture.detail.caseId === caseId)
  if (!found) {
    throw new HttpError(404, 'Case not found.')
  }
  return found
}

function redacted<T extends { patientDisplayName?: string | null; patientSecondaryIdentifier?: string | null }>(
  who: Reader,
  value: T,
): T {
  return who.role === 'coordinator' ? { ...value, patientDisplayName: null, patientSecondaryIdentifier: null } : value
}

function summaryOf(detail: CaseDetail): CaseSummary {
  return {
    caseId: detail.caseId,
    patientReference: detail.patientReference,
    patientDisplayName: detail.patientDisplayName,
    patientSecondaryIdentifier: detail.patientSecondaryIdentifier,
    facilityId: detail.facilityId,
    displayLabel: detail.displayLabel,
    closureReason: detail.closureReason,
    createdAt: detail.createdAt,
    pathwayId: detail.pathwayId,
    stage: detail.stage,
    urgency: detail.urgency,
    owner: detail.owner,
    nextAction: detail.nextAction,
    dueAt: detail.dueAt,
    isEscalated: detail.isEscalated,
    exceptions: [...detail.exceptions],
    updatedAt: detail.updatedAt,
  }
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

function scoped(state: MockState, who: Reader, facilityId: string | null): CaseDetail[] {
  const facility = facilityId ?? who.facilityId
  return state.cases
    .map((fixture) => fixture.detail)
    .filter((detail) => who.role !== 'coordinator' || hasOpenCoordinatorEscalation(detail))
    .filter((detail) => !facility || detail.facilityId === facility)
}

function listCases(state: MockState, who: Reader, query: URLSearchParams): CaseSummary[] {
  const stage = query.get('stage')
  const owner = query.get('ownerReference')
  const escalated = query.get('escalated')
  const patient = query.get('patientReference')
  const facilityId = query.get('facilityId')
  const skip = Number(query.get('skip') ?? 0)
  const take = Number(query.get('take') ?? 50)
  if (!Number.isInteger(skip) || skip < 0 || !Number.isInteger(take) || take < 1 || take > 200) {
    throw new HttpError(400, 'Invalid case filter or paging bounds.')
  }
  if (patient !== null && !PATIENT_SEARCH_ROLES.includes(who.role)) {
    throw new HttpError(403, 'Patient searches are restricted to clinician roles.')
  }
  checkOutsideTeam(who, facilityId, query.get('reason'))
  return scoped(state, who, facilityId)
    .filter((detail) => !stage || detail.stage === stage)
    .filter((detail) => !owner || detail.owner.reference === owner)
    .filter((detail) => escalated === null || String(detail.isEscalated) === escalated)
    .filter((detail) => patient === null || detail.patientReference === patient)
    .sort((a, b) => Date.parse(b.updatedAt) - Date.parse(a.updatedAt) || a.caseId.localeCompare(b.caseId))
    .slice(skip, skip + take)
    .map((detail) => redacted(who, summaryOf(detail)))
}

function listExceptions(state: MockState, who: Reader, query: URLSearchParams): Schemas['ExceptionItemResponse'][] {
  const facilityId = query.get('facilityId')
  const owner = query.get('ownerReference')
  checkOutsideTeam(who, facilityId, query.get('reason'))
  const now = mockNow().getTime()
  return scoped(state, who, facilityId)
    .filter((detail) => detail.stage !== 'Closed' && detail.exceptions.length > 0 && detail.nextAction && detail.dueAt)
    .filter((detail) => !owner || detail.owner.reference === owner)
    .sort((a, b) => Date.parse(a.dueAt ?? '') - Date.parse(b.dueAt ?? ''))
    .map((detail) => {
      const late = now - Date.parse(detail.dueAt ?? '')
      const open = detail.escalations.filter((escalation) => !escalation.resolvedAt)
      return redacted(who, {
        caseId: detail.caseId,
        patientReference: detail.patientReference,
        patientDisplayName: detail.patientDisplayName,
        patientSecondaryIdentifier: detail.patientSecondaryIdentifier,
        facilityId: detail.facilityId,
        pathwayId: detail.pathwayId,
        displayLabel: detail.displayLabel,
        exceptions: [...detail.exceptions],
        stage: detail.stage,
        urgency: detail.urgency,
        nextAction: detail.nextAction ?? '',
        owner: detail.owner,
        dueAt: detail.dueAt ?? '',
        overdueBy: late > 0 ? timeSpan(late) : null,
        isEscalated: detail.isEscalated,
        escalationLevel: Math.max(0, ...open.map((escalation) => Number(escalation.level))),
      })
    })
}

function acknowledge(fixture: CaseFixture, escalationId: string, who: Reader, reason: string | null): CaseDetail {
  const detail = fixture.detail
  const now = mockNow().toISOString()
  const record = (rejection?: string): void => {
    fixture.timeline.push({
      auditEventId: randomUUID(),
      sourceSystem: 'careloop',
      eventType: 'EscalationAcknowledged',
      disposition: rejection ? 'Rejected' : 'Applied',
      actorReference: who.reference,
      actorRole: who.role,
      sourceTimestamp: now,
      receivedTimestamp: now,
      priorStage: detail.stage,
      newStage: detail.stage,
      reason: rejection ?? 'Escalation acknowledged. The person responsible and the deadline stay the same.',
      evidenceReference: null,
    })
  }
  try {
    checkCase(who, detail, reason)
  } catch (error) {
    record('Escalation access is not permitted for this reader.')
    throw error
  }
  const target = detail.escalations.find((escalation) => escalation.escalationId === escalationId)
  if (target && target.escalatedTo.role !== who.role) {
    record('Only the role addressed by this escalation may acknowledge it.')
    throw new HttpError(403, 'Only the role addressed by this escalation may acknowledge it.')
  }
  const current = detail.escalations.filter((escalation) => !escalation.resolvedAt).at(-1)
  if (detail.stage === 'Closed' || !current || current.escalationId !== escalationId) {
    record('This escalation is not the current open escalation; it is unknown, resolved or superseded.')
    throw new HttpError(409, 'This escalation is not the current open escalation.')
  }
  if (current.acknowledgedAt) {
    record('This escalation was already acknowledged.')
    throw new HttpError(409, 'This escalation was already acknowledged.')
  }
  fixture.detail = {
    ...detail,
    escalations: detail.escalations.map((escalation) =>
      escalation.escalationId === escalationId
        ? { ...escalation, acknowledgedBy: who.reference, acknowledgedByRole: who.role, acknowledgedAt: now }
        : escalation,
    ),
    updatedAt: now,
  }
  record()
  return redacted(who, fixture.detail)
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

function openDraft(state: MockState, caseId: string, summaryId: string, who: Reader): HandoffSummary {
  if (!SUMMARY_REVIEWER_ROLES.includes(who.role)) {
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

function approve(state: MockState, fixture: CaseFixture, summary: HandoffSummary, who: Reader, text: string): HandoffSummary {
  const now = mockNow().toISOString()
  const approved: HandoffSummary = {
    ...summary,
    status: 'Approved',
    text,
    humanReviewRequired: false,
    approvedBy: who.reference,
    approvedAt: now,
  }
  state.summaries.set(summary.summaryId, approved)
  const detail = fixture.detail
  fixture.timeline.push({
    auditEventId: randomUUID(),
    sourceSystem: 'careloop',
    eventType: 'HandoffSummaryApproved',
    disposition: 'Applied',
    actorReference: who.reference,
    actorRole: who.role,
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

function reject(state: MockState, summary: HandoffSummary, who: Reader, reason: string): HandoffSummary {
  const rejected: HandoffSummary = {
    ...summary,
    status: 'Rejected',
    rejectedBy: who.reference,
    rejectedAt: mockNow().toISOString(),
    rejectionReason: reason,
  }
  state.summaries.set(summary.summaryId, rejected)
  return rejected
}

/** How far the patient-contact track has come, so a call outcome never moves it backwards. */
const CONTACT_RANK: Readonly<Record<string, number>> = { Acknowledged: 1, Understood: 2 }

/**
 * Records what a nurse navigator learned on a call, with the API's rules: nurse navigators only, at
 * least one outcome, a rating from 1 to 5, and only once a message has been sent to the patient.
 */
function recordCall(fixture: CaseFixture, who: Reader, reason: string | null, body: Record<string, unknown>): CaseDetail {
  if (who.role !== 'nurse-navigator') {
    throw new HttpError(403, 'Only a nurse navigator may record the outcome of a call with a patient.')
  }
  const acknowledged = body.acknowledged === true
  const understood = body.understood === true
  const rated = body.confidence !== null && body.confidence !== undefined
  const confidence = Number(body.confidence)
  if (!acknowledged && !understood && !rated) {
    throw new HttpError(400, 'Record at least one outcome.')
  }
  if (rated && !(Number.isInteger(confidence) && confidence >= 1 && confidence <= 5)) {
    throw new HttpError(400, 'A confidence rating must be between 1 and 5.')
  }
  const detail = fixture.detail
  checkCase(who, detail, reason)
  const now = mockNow().toISOString()
  const record = (text: string, refused = false): void => {
    fixture.timeline.push({
      auditEventId: randomUUID(),
      sourceSystem: detail.sourceSystem,
      eventType: 'NotificationUpdated',
      disposition: refused ? 'Rejected' : 'Applied',
      actorReference: who.reference,
      actorRole: who.role,
      sourceTimestamp: now,
      receivedTimestamp: now,
      priorStage: detail.stage,
      newStage: detail.stage,
      reason: text,
      evidenceReference: null,
    })
  }
  const contact = detail.tracks.find((entry) => entry.kind === 'Notification')
  if (!contact?.status) {
    record('No notification has been requested for this case, so there is nothing to report on.', true)
    throw new HttpError(409, 'No notification has been requested for this case, so there is nothing to report on.')
  }
  const advance = (status: 'Acknowledged' | 'Understood'): void => {
    if ((CONTACT_RANK[contact.status ?? ''] ?? 0) < (CONTACT_RANK[status] ?? 0)) {
      contact.status = status
      contact.isComplete = true
      contact.updatedAt = now
    }
  }
  if (acknowledged) {
    advance('Acknowledged')
    record('On a call, the patient said they will come.')
  }
  if (understood) {
    advance('Understood')
    record('On a call, the patient said the next step back in their own words.')
  }
  if (rated) {
    detail.confidence = confidence
    record(`On a call, the patient rated their confidence ${confidence} out of 5.`)
  }
  return detail
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
  const [first, second, third, fourth, fifth, ...rest] = segments
  const query = url.searchParams

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
  if (path === '/metrics/simulation') {
    expect('POST')
    const body = await readJson(req)
    const cohortScenario = requireText(body, 'cohortScenario')
    if (cohortScenario !== 'cohort') {
      throw new HttpError(400, `Unknown cohort scenario "${cohortScenario}".`)
    }
    const seed = body.seed === null || body.seed === undefined ? COHORT_SEED : Number(body.seed)
    send(res, 200, { cohortScenario, seed, withEscalation: WITH_ESCALATION, withoutEscalation: WITHOUT_ESCALATION })
    return true
  }
  const who = reader(req)
  if (path === '/cases') {
    expect('GET')
    send(res, 200, listCases(state, who, query))
    return true
  }
  if (path === '/exceptions') {
    expect('GET')
    send(res, 200, listExceptions(state, who, query))
    return true
  }
  if (path === '/metrics') {
    expect('GET')
    checkOutsideTeam(who, query.get('facilityId'), query.get('reason'))
    send(res, 200, createMetrics())
    return true
  }
  if (first === 'programmes' && third === undefined) {
    expect('GET')
    if (second === undefined) {
      send(res, 200, state.programmes)
      return true
    }
    const found = state.programmes.find((programme) => programme.pathwayId === second)
    if (!found) {
      throw new HttpError(404, 'Programme not found.')
    }
    send(res, 200, found)
    return true
  }
  if (first !== 'cases' || second === undefined || rest.length > 0) {
    return false
  }
  const caseId = second
  const fixture = findCase(state, caseId)
  const reason = query.get('reason')
  if (third === undefined) {
    expect('GET')
    checkCase(who, fixture.detail, reason)
    send(res, 200, redacted(who, fixture.detail))
    return true
  }
  if (third === 'timeline' && fourth === undefined) {
    expect('GET')
    checkCase(who, fixture.detail, reason)
    send(res, 200, fixture.timeline)
    return true
  }
  if (third === 'call-outcome' && fourth === undefined) {
    expect('POST')
    send(res, 200, redacted(who, recordCall(fixture, who, reason, await readJson(req))))
    return true
  }
  if (third === 'escalations' && fourth !== undefined && fifth === 'acknowledgement') {
    expect('POST')
    send(res, 200, acknowledge(fixture, fourth, who, reason))
    return true
  }
  if (third !== 'handoff-summary') {
    return false
  }
  if (fourth === undefined) {
    expect('POST')
    send(res, 201, draftSummary(state, fixture))
    return true
  }
  if (fifth === 'approval' || fifth === 'rejection') {
    expect('POST')
    const body = await readJson(req)
    const summary = openDraft(state, caseId, fourth, who)
    send(
      res,
      200,
      fifth === 'approval'
        ? approve(state, fixture, summary, who, requireText(body, 'approvedText'))
        : reject(state, summary, who, requireText(body, 'reason').trim()),
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
  const state: MockState = { cases: createCases(), summaries: new Map(), programmes: createProgrammes() }

  const handler: Connect.NextHandleFunction = (req, res) => {
    route(state, req, res)
      .then((handled) => {
        if (!handled) {
          send(res, 404, problem(404, 'No such path in the CareLoop API.'))
        }
      })
      .catch((error: unknown) => {
        if (error instanceof HttpError) {
          send(res, error.status, problem(error.status, error.message))
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
