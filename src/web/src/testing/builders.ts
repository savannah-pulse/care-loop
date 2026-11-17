/**
 * Builders of synthetic API responses for the unit tests.
 *
 * @remarks
 * Every person, patient and value is invented. Each builder returns a complete response as the API
 * would send it, with references written the way the API stores them, and takes overrides, so a
 * test states only what it is about. Nothing in the app imports this module.
 */

import type {
  AiText,
  CaseDetail,
  CaseSummary,
  Escalation,
  HandoffSummary,
  Metrics,
  Owner,
  Programme,
  RateMeasure,
  TimelineEntry,
} from '../data'

/** The moment the test cases describe: Thursday 12 November 2026, 09:00 in Abu Dhabi. */
export const TEST_NOW = new Date('2026-11-12T05:00:00Z')

/** The facility of the test cases. */
export const TEST_FACILITY = 'family-health-centre'

/**
 * Builds a case owner.
 *
 * @param overrides - Fields to change.
 * @returns Dr. Rekha Varghese, a clinician at the Family Health Centre, unless overridden.
 */
export function owner(overrides: Partial<Owner> = {}): Owner {
  return { reference: 'rekha-varghese', role: 'clinician', facilityId: TEST_FACILITY, isQueue: false, ...overrides }
}

/**
 * Builds the facility lead's queue at a facility, where cases without a named owner wait.
 *
 * @param facilityId - The facility.
 * @returns The queue as the API reports it.
 */
export function leadQueue(facilityId: string = TEST_FACILITY): Owner {
  return { reference: `facility-lead:${facilityId}`, role: 'facility-lead', facilityId, isQueue: true }
}

/**
 * Builds a case as `GET /cases` lists it.
 *
 * @param overrides - Fields to change.
 * @returns An open breast-screening case at follow-up, owned by Dr. Rekha and due in three hours,
 * unless overridden.
 */
export function caseSummary(overrides: Partial<CaseSummary> = {}): CaseSummary {
  return {
    caseId: '3f6b1c2a-8d4e-4f5a-9b7c-100245891000',
    patientReference: 'p-100245891',
    patientDisplayName: 'Hessa Al Dhaheri',
    patientSecondaryIdentifier: '100245891',
    facilityId: TEST_FACILITY,
    displayLabel: 'FollowUp',
    closureReason: null,
    createdAt: '2026-11-09T06:00:00Z',
    pathwayId: 'breast-screening',
    stage: 'FollowUpActive',
    urgency: 'PLACEHOLDER-URGENCY-A',
    owner: owner(),
    nextAction: 'arrange-follow-up',
    dueAt: '2026-11-12T08:00:00Z',
    isEscalated: false,
    exceptions: [],
    updatedAt: '2026-11-11T11:00:00Z',
    ...overrides,
  }
}

/**
 * Builds a case as `GET /cases/{caseId}` returns it.
 *
 * @param overrides - Fields to change.
 * @returns The case of {@link caseSummary} in full, with no tracks and no escalations, unless
 * overridden.
 */
export function caseDetail(overrides: Partial<CaseDetail> = {}): CaseDetail {
  return {
    ...caseSummary(),
    sourceSystem: 'fhc-his',
    sourceResultId: 'DiagnosticReport/r-100245891',
    escalations: [],
    outcomeEvidenceKind: null,
    confidence: null,
    proposedOwner: null,
    tracks: [],
    outcomeEvidenceReference: null,
    ...overrides,
  }
}

/**
 * Builds an escalation.
 *
 * @param overrides - Fields to change.
 * @returns An open, unacknowledged level-1 escalation to the facility lead's queue, raised at
 * 15:00 on Wednesday 11 November in Abu Dhabi, unless overridden.
 */
export function escalation(overrides: Partial<Escalation> = {}): Escalation {
  return {
    escalationId: '5d1e2f3a-4b5c-4d6e-8f70-100245891e01',
    level: 1,
    escalatedTo: leadQueue(),
    raisedAt: '2026-11-11T11:00:00Z',
    reason: 'Deadline elapsed for next action arrange-follow-up.',
    acknowledgedBy: null,
    acknowledgedByRole: null,
    acknowledgedAt: null,
    resolvedAt: null,
    ...overrides,
  }
}

/**
 * Builds a timeline entry.
 *
 * @param overrides - Fields to change.
 * @returns An entry CareLoop applied, recorded by CareLoop itself, unless overridden.
 */
export function timelineEntry(overrides: Partial<TimelineEntry> = {}): TimelineEntry {
  return {
    auditEventId: 'a0000000-0000-4000-8000-000000000001',
    sourceSystem: 'careloop',
    eventType: 'DeadlineElapsed',
    disposition: 'Applied',
    actorReference: 'careloop',
    actorRole: 'system',
    sourceTimestamp: '2026-11-11T05:00:00Z',
    receivedTimestamp: '2026-11-11T05:00:00Z',
    priorStage: 'FollowUpActive',
    newStage: 'FollowUpActive',
    reason: 'Deadline warning raised',
    evidenceReference: null,
    origin: 'careloop',
    ...overrides,
  }
}

/**
 * Builds a programme's settings, as `GET /programmes/{pathwayId}` returns them.
 *
 * @param overrides - Fields to change.
 * @returns A placeholder breast-screening programme with one follow-up deadline (one day, reminder
 * six hours before) and a two-level ladder: the facility lead at the deadline, the programme
 * coordinator a day later. Nothing is clinically approved, unless overridden.
 */
export function programme(overrides: Partial<Programme> = {}): Programme {
  return {
    pathwayId: 'breast-screening',
    version: '0.0.1-placeholder',
    displayName: 'PLACEHOLDER - Breast screening follow-up',
    isPlaceholder: true,
    urgencyCodes: ['PLACEHOLDER-URGENCY-A', 'PLACEHOLDER-URGENCY-B'],
    unassignedQueueRole: 'facility-lead',
    stages: [],
    tracks: [],
    deadlines: [
      {
        nextAction: 'arrange-follow-up',
        urgencyCode: null,
        duration: '1.00:00:00',
        warnBefore: '06:00:00',
        clinicallyApproved: false,
        ownerRole: 'clinician',
        citation: 'PLACEHOLDER - no clinical source yet',
      },
    ],
    escalationLadder: [
      { level: 1, escalateToRole: 'facility-lead', afterOverdue: '00:00:00', clinicallyApproved: false },
      { level: 2, escalateToRole: 'coordinator', afterOverdue: '1.00:00:00', clinicallyApproved: false },
    ],
    closureRules: [],
    evidenceKinds: [],
    changeLog: [],
    ...overrides,
  }
}

/**
 * Builds a handoff summary draft, as `POST /cases/{caseId}/handoff-summary` returns it.
 *
 * @param overrides - Fields to change.
 * @returns A draft for the case of {@link caseSummary}, drafted at {@link TEST_NOW}, with three
 * sections (the situation, the assessment AI leaves to the clinician and a recommendation) run
 * together as its text, and one fact for the reviewer to check, unless overridden.
 */
export function handoffSummary(overrides: Partial<HandoffSummary> = {}): HandoffSummary {
  const sections = [
    {
      heading: 'Situation',
      text: 'Screening found a change that needs further assessment.',
      sourceFactIds: ['screening-report-2026-11-10'],
    },
    { heading: 'Assessment', text: 'For the clinician to write.', sourceFactIds: [] },
    {
      heading: 'Recommendation',
      text: 'Book the assessment clinic at the General Hospital.',
      sourceFactIds: ['referral-declined-2026-11-10'],
    },
  ]
  return {
    summaryId: '7e8f9a0b-1c2d-4e3f-8a4b-100245891501',
    caseId: caseSummary().caseId,
    status: 'Draft',
    sections,
    text: sections.map((section) => `${section.heading}\n${section.text}`).join('\n\n'),
    missingOrConflictingFacts: ['The referral does not say which clinic the patient prefers.'],
    humanReviewRequired: true,
    draftedBy: 'careloop-ai',
    modelDeployment: 'synthetic-model',
    promptVersion: 'handoff-v1',
    trigger: 'manual',
    language: 'en',
    draftedAt: TEST_NOW.toISOString(),
    approvedBy: null,
    approvedAt: null,
    rejectedBy: null,
    rejectedAt: null,
    rejectionReason: null,
    ...overrides,
  }
}

function rate(numerator: number, denominator: number): RateMeasure {
  return { numerator, denominator, rate: denominator === 0 ? null : numerator / denominator }
}

/**
 * Builds the loop-closure numbers, as `GET /metrics` returns them.
 *
 * @param overrides - Fields to change.
 * @returns The numbers of a team with 24 cases, 20 of them open and 3 past their deadline, every
 * measure with what it counts and what it is out of, unless overridden.
 */
export function metrics(overrides: Partial<Metrics> = {}): Metrics {
  return {
    generatedAt: TEST_NOW.toISOString(),
    totalCases: 24,
    openCases: 20,
    closedCases: 4,
    casesByStage: { ReviewPending: 6, FollowUpActive: 12, ClosureReview: 2 },
    casesByException: { Overdue: 3, Rejected: 1, Unreachable: 1, Unacknowledged: 0, Unassigned: 1 },
    overdueCases: 3,
    escalatedCases: 1,
    unassignedCases: 1,
    acknowledgementRate: 0.8,
    closureRate: 4 / 24,
    medianTimeToClosure: '5.00:00:00',
    measures: {
      acknowledgement: rate(16, 20),
      closure: rate(4, 24),
      understanding: rate(12, 16),
      nextStepOnTime: rate(17, 20),
      referralAcceptedAndAttended: rate(5, 6),
      appointmentsAttended: rate(9, 10),
      treatmentStartedOnTime: rate(2, 3),
      signedEvidence: rate(4, 4),
    },
    timeToStep: {
      acknowledgement: { denominator: 16, median: '1.04:00:00' },
      consultation: { denominator: 8, median: '2.00:00:00' },
      appointment: { denominator: 9, median: '6.00:00:00' },
      careStarted: { denominator: 3, median: '9.00:00:00' },
      closure: { denominator: 4, median: '5.00:00:00' },
    },
    closuresByReason: { Treated: 3, NegativeOnConfirmation: 1 },
    casesReachingDisplayLabel: { CaseOpened: rate(24, 24), FollowUp: rate(14, 24) },
    casesByDisplayLabel: { OwnerAssigned: 6, PatientNotified: 4, FollowUp: 8, ClosureReview: 2, Closed: 4 },
    confidenceRate: 0.25,
    confidenceDenominator: 20,
    confidenceResponses: 5,
    averageConfidence: 3.8,
    ...overrides,
  }
}

/**
 * Builds words an AI agent wrote, as the three AI endpoints return them.
 *
 * @param overrides - Fields to change.
 * @returns One sentence from the role-briefing agent, received at {@link TEST_NOW}, unless overridden.
 */
export function aiText(overrides: Partial<AiText> = {}): AiText {
  return {
    agent: 'role-briefing',
    text: 'One case is late and already escalated, so start there.',
    generatedAt: TEST_NOW.toISOString(),
    ...overrides,
  }
}
