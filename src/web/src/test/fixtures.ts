import { vi } from 'vitest'
import type { Mock } from 'vitest'
import type { CareLoopApi, CaseDetail, CaseSummary, ExceptionItem, HandoffSummary, Programme, StaffIdentity, TimelineEntry } from '../api/client'

/** A synthetic clinician identity for component tests. */
export const clinician: StaffIdentity = { user: 'SYN-STAFF-1', role: 'clinician', facility: 'SYN-FACILITY-1' }

/** A synthetic programme coordinator identity, which never sees names. */
export const coordinator: StaffIdentity = { user: 'SYN-COORD-1', role: 'coordinator', facility: '' }

/** A synthetic facility lead identity, the addressee of the fixture escalation. */
export const facilityLead: StaffIdentity = { user: 'SYN-LEAD-1', role: 'facility-lead', facility: 'SYN-FACILITY-1' }

/**
 * Builds a synthetic case detail with one open escalation addressed to the facility lead.
 * @param overrides - Fields to replace.
 * @returns A complete case detail containing no real data.
 */
export function detail(overrides: Partial<CaseDetail> = {}): CaseDetail {
  return {
    caseId: 'case-1', sourceSystem: 'SYN-HIS', sourceResultId: 'SYN-RESULT-1', patientReference: 'Patient/SYN-1',
    patientDisplayName: 'Synthetic Patient', patientSecondaryIdentifier: 'SYN-MRN-1', displayLabel: 'PatientNotified',
    escalations: [{
      escalationId: 'esc-1', level: 1, escalatedTo: { reference: 'SYN-LEAD-1', role: 'facility-lead', facilityId: 'SYN-FACILITY-1', isQueue: false },
      raisedAt: '2026-01-02T00:00:00Z', reason: 'Synthetic overdue reason',
    }],
    pathwayId: 'breast-screening', facilityId: 'SYN-FACILITY-1', stage: 'FollowUpActive', urgency: 'source-reviewed',
    owner: { reference: 'SYN-STAFF-1', role: 'clinician', facilityId: 'SYN-FACILITY-1', isQueue: false },
    nextAction: 'review-result', dueAt: '2026-01-03T00:00:00Z', isEscalated: true, exceptions: ['Overdue'],
    tracks: [{ kind: 'Notification', status: 'Sent', isComplete: false, updatedAt: '2026-01-01T00:00:00Z' }],
    createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-02T00:00:00Z', ...overrides,
  }
}

/**
 * Builds a synthetic list row.
 * @param caseId - The case identifier.
 * @param overrides - Fields to replace.
 * @returns A complete list row containing no real data.
 */
export function summary(caseId: string, overrides: Partial<CaseSummary> = {}): CaseSummary {
  return {
    caseId, patientReference: `Patient/${caseId}`, patientDisplayName: `Synthetic ${caseId}`, patientSecondaryIdentifier: null,
    facilityId: 'SYN-FACILITY-1', displayLabel: 'PatientNotified', createdAt: '2026-01-01T00:00:00Z', pathwayId: 'breast-screening',
    stage: 'FollowUpActive', urgency: null, owner: { reference: 'SYN-STAFF-1', role: 'clinician', facilityId: 'SYN-FACILITY-1', isQueue: false },
    nextAction: 'review-result', dueAt: '2026-01-03T00:00:00Z', isEscalated: false, exceptions: [], updatedAt: '2026-01-02T00:00:00Z',
    ...overrides,
  }
}

/**
 * Builds a synthetic exception-queue row.
 * @param caseId - The case identifier.
 * @param overrides - Fields to replace, for example a missing deadline.
 * @returns A complete exception row containing no real data.
 */
export function exceptionItem(caseId: string, overrides: Partial<ExceptionItem> = {}): ExceptionItem {
  const { updatedAt: _updatedAt, ...row } = summary(caseId)
  return { ...row, exceptions: ['Unassigned'], overdueBy: null, escalationLevel: 0, ...overrides }
}

/** A synthetic pathway definition with one required track and one ladder step. */
export const programme: Programme = {
  pathwayId: 'breast-screening', version: 'SYN-1', displayName: 'Synthetic breast pathway', isPlaceholder: true, urgencyCodes: [],
  unassignedQueueRole: 'facility-lead', stages: [], deadlines: [], closureRules: [], evidenceKinds: [], changeLog: [],
  tracks: [{ kind: 'Referral', requiredForClosure: true, nextActionOnException: 'arrange-alternative-referral' }],
  escalationLadder: [{ level: 1, escalateToRole: 'facility-lead', afterOverdue: '01:00:00', clinicallyApproved: false }],
}

/** A synthetic timeline entry. */
export const timelineEntry: TimelineEntry = {
  auditEventId: 'audit-1', sourceSystem: 'SYN-HIS', eventType: 'ScreeningResultReceived', disposition: 'Applied',
  actorReference: 'SYN-HIS', actorRole: 'system', sourceTimestamp: '2026-01-01T00:00:00Z', receivedTimestamp: '2026-01-01T00:00:01Z',
  newStage: 'Received', reason: 'Synthetic result', evidenceReference: null,
}

/**
 * Builds a synthetic handoff draft.
 * @param overrides - Fields to replace.
 * @returns A complete draft containing no real data.
 */
export function handoffSummary(overrides: Partial<HandoffSummary> = {}): HandoffSummary {
  return {
    summaryId: 'draft-1', caseId: 'case-1', status: 'Draft', sections: [{ heading: 'Facts', text: 'Synthetic fact', sourceFactIds: ['fact-1'] }],
    text: 'Synthetic draft text.', missingOrConflictingFacts: [], humanReviewRequired: true, draftedBy: 'SYN-AI', modelDeployment: 'SYN-MODEL',
    promptVersion: 'SYN-PROMPT', trigger: 'manual', language: 'en', draftedAt: '2026-01-02T00:00:00Z', ...overrides,
  }
}

/** A mocked API client: every operation is a mock function that can be asserted on. */
export type MockApi = CareLoopApi & { [K in keyof CareLoopApi]: CareLoopApi[K] & Mock }

/**
 * Builds a mocked API client whose operations resolve with synthetic data.
 * @param overrides - Mock implementations to replace.
 * @returns A client of mock functions.
 */
export function fakeApi(overrides: Partial<Record<keyof CareLoopApi, Mock>> = {}): MockApi {
  return {
    cases: vi.fn().mockResolvedValue([]),
    exceptions: vi.fn().mockResolvedValue([]),
    detail: vi.fn().mockResolvedValue(detail()),
    timeline: vi.fn().mockResolvedValue([timelineEntry]),
    metrics: vi.fn(),
    programmes: vi.fn().mockResolvedValue([]),
    programme: vi.fn().mockResolvedValue(programme),
    acknowledge: vi.fn().mockResolvedValue(detail()),
    draft: vi.fn().mockResolvedValue(handoffSummary()),
    approve: vi.fn().mockResolvedValue(handoffSummary({ status: 'Approved' })),
    reject: vi.fn().mockResolvedValue(handoffSummary({ status: 'Rejected' })),
    ...overrides,
  } as unknown as MockApi
}
