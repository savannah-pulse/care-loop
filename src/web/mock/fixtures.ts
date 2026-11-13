/**
 * Fixtures for the local mock API: the eight worklist cases of UI design board 1, Hessa's case and
 * timeline from board 2, loop-closure numbers from board 3 and the handoff summary draft from
 * board 18. Every person, reference and number is invented. Typed against the generated API schema,
 * so a contract change that breaks them fails the type check.
 *
 * Times are written as on the boards, in Abu Dhabi time on Thursday 12 November 2026 at 09:00, and
 * moved so that moment falls when the dev server starts. Set `CARELOOP_MOCK_NOW` (an ISO 8601
 * date-time) to pin that moment instead, for example to reproduce the boards exactly; see
 * {@link startScenarioClock}.
 */

import type { components } from '../src/api/schema.d.ts'

type Schemas = components['schemas']

/** A case as `GET /cases/{caseId}` returns it. */
export type CaseDetail = Schemas['CaseDetailResponse']
/** One timeline entry as `GET /cases/{caseId}/timeline` returns it. */
export type TimelineEntry = Schemas['TimelineEntryResponse']
/** A handoff summary as the summary endpoints return it. */
export type HandoffSummary = Schemas['HandoffSummaryResponse']
/** Loop-closure metrics as `GET /metrics` returns them. */
export type Metrics = Schemas['MetricsResponse']
/** One arm of the escalation comparison. */
export type SimulationArm = Schemas['SimulationArmResponse']

type Owner = Schemas['OwnerResponse']
type Stage = Schemas['CaseStage']
type EventType = Schemas['CaseEventType']
type Track = Schemas['TrackStatusResponse']

/** The moment the fixtures describe: Thursday 12 November 2026, 09:00 in Abu Dhabi. */
export const SCENARIO_TIME = Date.parse('2026-11-12T09:00:00+04:00')

let startedAt = Date.now()
let anchor = startedAt
let shift = anchor - SCENARIO_TIME

/**
 * Starts the mock API's clock, deciding where the scenario moment falls on the real clock.
 *
 * @param pinnedNow - An ISO 8601 date-time to map the scenario moment to (`CARELOOP_MOCK_NOW`);
 * when absent or unreadable, the scenario moment is now.
 */
export function startScenarioClock(pinnedNow?: string): void {
  const pinned = Date.parse(pinnedNow ?? '')
  startedAt = Date.now()
  anchor = Number.isNaN(pinned) ? startedAt : pinned
  shift = anchor - SCENARIO_TIME
}

/**
 * The mock API's clock: the scenario moment plus however long the server has been running.
 *
 * @returns The current time as the fixtures see it.
 */
export function mockNow(): Date {
  return new Date(anchor + (Date.now() - startedAt))
}

/**
 * Converts a time written as on the boards into the time the mock API reports.
 *
 * @param boardTime - An ISO 8601 date-time with its offset, for example `2026-11-11T15:00:00+04:00`.
 * @returns The same moment moved by the gap between the scenario moment and the server start, in UTC.
 */
export function at(boardTime: string): string {
  return new Date(Date.parse(boardTime) + shift).toISOString()
}

const FACILITY = 'family-health-centre'

const REKHA: Owner = { reference: 'Practitioner/rekha-varghese', role: 'clinician', facilityId: FACILITY, isQueue: false }
const HUDA: Owner = { reference: 'Practitioner/huda-saleh', role: 'nurse-navigator', facilityId: FACILITY, isQueue: false }
const SARA: Owner = { reference: 'Practitioner/sara-al-hammadi', role: 'clinician', facilityId: FACILITY, isQueue: false }
const LEAD_QUEUE: Owner = {
  reference: 'queue/facility-lead/family-health-centre',
  role: 'facility-lead',
  facilityId: FACILITY,
  isQueue: true,
}
const ENDOSCOPY_UNIT: Owner = {
  reference: 'Organization/endoscopy-unit-general-hospital',
  role: 'receiving-clinician',
  facilityId: 'general-hospital',
  isQueue: false,
}

const SYSTEM = { actor: 'careloop', role: 'system' } as const
const BY_REKHA = { actor: REKHA.reference, role: REKHA.role } as const
const BY_HUDA = { actor: HUDA.reference, role: HUDA.role } as const
const BY_SARA = { actor: SARA.reference, role: SARA.role } as const

/** The urgency code every shared scenario uses for routine reviews. */
const ROUTINE = 'PLACEHOLDER-URGENCY-A'
/** The urgency code shown as urgent. */
const URGENT = 'PLACEHOLDER-URGENCY-B'

function track(kind: Track['kind'], status: string | null, isComplete: boolean, boardTime?: string): Track {
  return { kind, status, isComplete, updatedAt: boardTime ? at(boardTime) : null }
}

function notStarted(kind: Track['kind']): Track {
  return track(kind, null, false)
}

interface EntrySpec {
  readonly type: EventType
  readonly time: string
  readonly by: { readonly actor: string; readonly role: string }
  readonly from: Stage | null
  readonly to: Stage
  readonly reason: string
  readonly evidence?: string
  readonly refused?: boolean
}

function entries(caseId: string, specs: readonly EntrySpec[]): TimelineEntry[] {
  return specs.map((spec, index) => {
    const source = at(spec.time)
    return {
      auditEventId: `${caseId.slice(0, -3)}${String(index + 1).padStart(3, '0')}`,
      eventType: spec.type,
      disposition: spec.refused ? 'Rejected' : 'Applied',
      actorReference: spec.by.actor,
      actorRole: spec.by.role,
      sourceTimestamp: source,
      receivedTimestamp: new Date(Date.parse(source) + 4000).toISOString(),
      priorStage: spec.from,
      newStage: spec.to,
      reason: spec.reason,
      evidenceReference: spec.evidence ?? null,
    }
  })
}

/** A case with its timeline. The mock changes both as people approve summaries. */
export interface CaseFixture {
  /** The case. */
  detail: CaseDetail
  /** Its audit timeline, oldest first. */
  readonly timeline: TimelineEntry[]
}

/** Hessa Al Dhaheri's case: the referral was declined, the new one is 18 hours late and escalated. */
export const HESSA_CASE_ID = '3f6b1c2a-8d4e-4f5a-9b7c-100245891000'

function hessa(): CaseFixture {
  const caseId = HESSA_CASE_ID
  return {
    detail: {
      caseId,
      sourceSystem: 'fhc-his',
      sourceResultId: 'MAMMO-2026-110941',
      patientReference: 'Patient/p-100245891',
      pathwayId: 'breast-screening',
      facilityId: FACILITY,
      stage: 'FollowUpActive',
      urgency: ROUTINE,
      owner: REKHA,
      proposedOwner: null,
      nextAction: 'arrange-alternative-referral',
      dueAt: at('2026-11-11T15:00:00+04:00'),
      isEscalated: true,
      exceptions: ['Overdue', 'Rejected'],
      tracks: [
        track('Notification', 'Understood', true, '2026-11-10T11:20:00+04:00'),
        track('Consultation', 'OutcomeRecorded', true, '2026-11-10T11:40:00+04:00'),
        track('Referral', 'Rejected', false, '2026-11-10T15:00:00+04:00'),
        notStarted('Appointment'),
      ],
      outcomeEvidenceReference: null,
      closureReason: null,
      createdAt: at('2026-11-10T09:00:00+04:00'),
      updatedAt: at('2026-11-11T15:00:00+04:00'),
    },
    timeline: entries(caseId, [
      {
        type: 'ScreeningResultReceived',
        time: '2026-11-10T09:00:00+04:00',
        by: BY_REKHA,
        from: null,
        to: 'ReviewPending',
        reason: 'Screening result needs further assessment. Owner named by the programme rules.',
        evidence: 'DiagnosticReport/mammo-2026-110941',
      },
      {
        type: 'ConsentRecorded',
        time: '2026-11-10T09:00:30+04:00',
        by: BY_SARA,
        from: 'ReviewPending',
        to: 'ReviewPending',
        reason: 'Her son may receive appointment details, with her written consent signed on Mon 9 Nov.',
        evidence: 'Consent/form-4471',
      },
      {
        type: 'ClinicalReviewCompleted',
        time: '2026-11-10T09:30:00+04:00',
        by: BY_REKHA,
        from: 'ReviewPending',
        to: 'FollowUpActive',
        reason: 'Reviewed as routine. Further assessment needed.',
      },
      {
        type: 'NotificationUpdated',
        time: '2026-11-10T10:00:00+04:00',
        by: SYSTEM,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Text delivered asking her to expect a call from her care team.',
      },
      {
        type: 'NotificationUpdated',
        time: '2026-11-10T11:15:00+04:00',
        by: BY_HUDA,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Result explained by phone, in Arabic.',
      },
      {
        type: 'NotificationUpdated',
        time: '2026-11-10T11:20:00+04:00',
        by: BY_HUDA,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Next step understood: she said it back on the call.',
      },
      {
        type: 'ConsultationUpdated',
        time: '2026-11-10T11:40:00+04:00',
        by: BY_REKHA,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Consultation outcome recorded: refer for breast assessment.',
      },
      {
        type: 'ReferralUpdated',
        time: '2026-11-10T12:00:00+04:00',
        by: BY_REKHA,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Referral sent to the Breast Clinic, Regional Hospital.',
      },
      {
        type: 'ReferralUpdated',
        time: '2026-11-10T15:00:00+04:00',
        by: { actor: 'Organization/breast-clinic-regional-hospital', role: 'receiving-clinician' },
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Referral declined by the Breast Clinic, Regional Hospital: no capacity.',
      },
      {
        type: 'DeadlineElapsed',
        time: '2026-11-11T11:00:00+04:00',
        by: SYSTEM,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Reminder sent to Dr. Rekha Varghese: the new referral is due at 15:00.',
      },
      {
        type: 'DeadlineElapsed',
        time: '2026-11-11T15:00:00+04:00',
        by: SYSTEM,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'New referral not sent by the deadline. Escalated to the facility lead.',
      },
    ]),
  }
}

function noura(): CaseFixture {
  const caseId = '7a2d4e6f-1b3c-4d5e-8f90-100318402000'
  return {
    detail: {
      caseId,
      sourceSystem: 'fhc-his',
      sourceResultId: 'MAMMO-2026-111207',
      patientReference: 'Patient/p-100318402',
      pathwayId: 'breast-screening',
      facilityId: FACILITY,
      stage: 'FollowUpActive',
      urgency: URGENT,
      owner: REKHA,
      proposedOwner: null,
      nextAction: 'answer-consultation',
      dueAt: at('2026-11-12T12:00:00+04:00'),
      isEscalated: false,
      exceptions: [],
      tracks: [
        track('Notification', 'Acknowledged', true, '2026-11-12T08:10:00+04:00'),
        track('Consultation', 'Requested', false, '2026-11-12T08:30:00+04:00'),
        notStarted('Referral'),
        notStarted('Appointment'),
      ],
      outcomeEvidenceReference: null,
      closureReason: null,
      createdAt: at('2026-11-12T07:30:00+04:00'),
      updatedAt: at('2026-11-12T08:30:00+04:00'),
    },
    timeline: entries(caseId, [
      {
        type: 'ScreeningResultReceived',
        time: '2026-11-12T07:30:00+04:00',
        by: BY_REKHA,
        from: null,
        to: 'ReviewPending',
        reason: 'Screening result needs further assessment.',
        evidence: 'DiagnosticReport/mammo-2026-111207',
      },
      {
        type: 'ClinicalReviewCompleted',
        time: '2026-11-12T07:45:00+04:00',
        by: BY_REKHA,
        from: 'ReviewPending',
        to: 'FollowUpActive',
        reason: 'Reviewed as urgent.',
      },
      {
        type: 'NotificationUpdated',
        time: '2026-11-12T08:10:00+04:00',
        by: BY_HUDA,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Result explained by phone. She acknowledged the next step.',
      },
      {
        type: 'ConsultationUpdated',
        time: '2026-11-12T08:30:00+04:00',
        by: BY_REKHA,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Consultation requested: agree the next step with her today.',
      },
    ]),
  }
}

function salem(): CaseFixture {
  const caseId = '9c4e6a8b-2d1f-4a3b-9c5d-100377615000'
  return {
    detail: {
      caseId,
      sourceSystem: 'fhc-his',
      sourceResultId: 'LDCT-2026-110388',
      patientReference: 'Patient/p-100377615',
      pathwayId: 'lung-screening',
      facilityId: FACILITY,
      stage: 'FollowUpActive',
      urgency: ROUTINE,
      owner: HUDA,
      proposedOwner: null,
      nextAction: 'call-patient',
      dueAt: at('2026-11-12T10:40:00+04:00'),
      isEscalated: false,
      exceptions: [],
      tracks: [
        track('Notification', 'Acknowledged', true, '2026-11-11T16:20:00+04:00'),
        notStarted('Consultation'),
        notStarted('Referral'),
        notStarted('Appointment'),
      ],
      outcomeEvidenceReference: null,
      closureReason: null,
      createdAt: at('2026-11-10T10:00:00+04:00'),
      updatedAt: at('2026-11-12T08:40:00+04:00'),
    },
    timeline: entries(caseId, [
      {
        type: 'ScreeningResultReceived',
        time: '2026-11-10T10:00:00+04:00',
        by: BY_SARA,
        from: null,
        to: 'ReviewPending',
        reason: 'Screening result needs follow-up.',
        evidence: 'DiagnosticReport/ldct-2026-110388',
      },
      {
        type: 'ClinicalReviewCompleted',
        time: '2026-11-10T14:00:00+04:00',
        by: BY_SARA,
        from: 'ReviewPending',
        to: 'FollowUpActive',
        reason: 'Reviewed as routine.',
      },
      {
        type: 'NotificationUpdated',
        time: '2026-11-11T16:20:00+04:00',
        by: SYSTEM,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Acknowledged in his health app.',
      },
      {
        type: 'NotificationUpdated',
        time: '2026-11-12T08:40:00+04:00',
        by: SYSTEM,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'He asked for a call back.',
      },
    ]),
  }
}

function khalid(): CaseFixture {
  const caseId = 'b1d3f5a7-3e2c-4b4d-8e6f-100402233000'
  return {
    detail: {
      caseId,
      sourceSystem: 'fhc-his',
      sourceResultId: 'FIT-2026-110514',
      patientReference: 'Patient/p-100402233',
      pathwayId: 'bowel-screening',
      facilityId: FACILITY,
      stage: 'ReviewPending',
      urgency: null,
      owner: LEAD_QUEUE,
      proposedOwner: null,
      nextAction: 'assign-owner',
      dueAt: at('2026-11-12T12:00:00+04:00'),
      isEscalated: false,
      exceptions: ['Unassigned'],
      tracks: [notStarted('Notification'), notStarted('Consultation'), notStarted('Referral'), notStarted('Appointment')],
      outcomeEvidenceReference: null,
      closureReason: null,
      createdAt: at('2026-11-11T12:00:00+04:00'),
      updatedAt: at('2026-11-12T08:00:00+04:00'),
    },
    timeline: entries(caseId, [
      {
        type: 'ScreeningResultReceived',
        time: '2026-11-11T12:00:00+04:00',
        by: BY_SARA,
        from: null,
        to: 'ReviewPending',
        reason: 'Screening result needs follow-up.',
        evidence: 'DiagnosticReport/fit-2026-110514',
      },
      {
        type: 'DeadlineElapsed',
        time: '2026-11-12T08:00:00+04:00',
        by: SYSTEM,
        from: 'ReviewPending',
        to: 'ReviewPending',
        reason: 'The named owner is on leave. The case is with the facility lead until someone accepts it.',
      },
    ]),
  }
}

function maricel(): CaseFixture {
  const caseId = 'd2e4a6c8-4f3d-4c5e-9f70-100455790000'
  return {
    detail: {
      caseId,
      sourceSystem: 'fhc-his',
      sourceResultId: 'HPV-2026-110622',
      patientReference: 'Patient/p-100455790',
      pathwayId: 'cervical-screening',
      facilityId: FACILITY,
      stage: 'FollowUpActive',
      urgency: ROUTINE,
      owner: HUDA,
      proposedOwner: null,
      nextAction: 'contact-patient-another-way',
      dueAt: at('2026-11-12T17:00:00+04:00'),
      isEscalated: false,
      exceptions: ['Unreachable'],
      tracks: [
        track('Notification', 'Failed', false, '2026-11-11T16:40:00+04:00'),
        notStarted('Consultation'),
        notStarted('Referral'),
        notStarted('Appointment'),
      ],
      outcomeEvidenceReference: null,
      closureReason: null,
      createdAt: at('2026-11-10T12:00:00+04:00'),
      updatedAt: at('2026-11-11T16:40:00+04:00'),
    },
    timeline: entries(caseId, [
      {
        type: 'ScreeningResultReceived',
        time: '2026-11-10T12:00:00+04:00',
        by: BY_SARA,
        from: null,
        to: 'ReviewPending',
        reason: 'Screening result needs follow-up.',
        evidence: 'DiagnosticReport/hpv-2026-110622',
      },
      {
        type: 'ClinicalReviewCompleted',
        time: '2026-11-10T15:00:00+04:00',
        by: BY_SARA,
        from: 'ReviewPending',
        to: 'FollowUpActive',
        reason: 'Reviewed as routine.',
      },
      {
        type: 'NotificationUpdated',
        time: '2026-11-11T10:15:00+04:00',
        by: BY_HUDA,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Call not answered.',
      },
      {
        type: 'NotificationUpdated',
        time: '2026-11-11T16:40:00+04:00',
        by: BY_HUDA,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Second call not answered. She is free between 13:00 and 14:00.',
      },
    ]),
  }
}

function nasreen(): CaseFixture {
  const caseId = 'e3f5b7d9-5a4e-4d6f-8a81-100468104000'
  return {
    detail: {
      caseId,
      sourceSystem: 'fhc-his',
      sourceResultId: 'MAMMO-2026-110903',
      patientReference: 'Patient/p-100468104',
      pathwayId: 'breast-screening',
      facilityId: FACILITY,
      stage: 'FollowUpActive',
      urgency: ROUTINE,
      owner: HUDA,
      proposedOwner: null,
      nextAction: 'acknowledge-notification',
      dueAt: at('2026-11-12T17:00:00+04:00'),
      isEscalated: false,
      exceptions: ['Unacknowledged'],
      tracks: [
        track('Notification', 'Delivered', false, '2026-11-10T10:00:00+04:00'),
        notStarted('Consultation'),
        notStarted('Referral'),
        notStarted('Appointment'),
      ],
      outcomeEvidenceReference: null,
      closureReason: null,
      createdAt: at('2026-11-09T14:00:00+04:00'),
      updatedAt: at('2026-11-12T08:00:00+04:00'),
    },
    timeline: entries(caseId, [
      {
        type: 'ScreeningResultReceived',
        time: '2026-11-09T14:00:00+04:00',
        by: BY_REKHA,
        from: null,
        to: 'ReviewPending',
        reason: 'Screening result needs further assessment.',
        evidence: 'DiagnosticReport/mammo-2026-110903',
      },
      {
        type: 'ClinicalReviewCompleted',
        time: '2026-11-09T16:00:00+04:00',
        by: BY_REKHA,
        from: 'ReviewPending',
        to: 'FollowUpActive',
        reason: 'Reviewed as routine.',
      },
      {
        type: 'NotificationUpdated',
        time: '2026-11-10T10:00:00+04:00',
        by: SYSTEM,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Text delivered.',
      },
      {
        type: 'DeadlineElapsed',
        time: '2026-11-12T08:00:00+04:00',
        by: SYSTEM,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'No reply to the text for two working days. Call her with an Urdu interpreter.',
      },
    ]),
  }
}

function rashid(): CaseFixture {
  const caseId = 'f4a6c8e0-6b5f-4e7a-9b92-100512337000'
  return {
    detail: {
      caseId,
      sourceSystem: 'fhc-his',
      sourceResultId: 'FIT-2026-110207',
      patientReference: 'Patient/p-100512337',
      pathwayId: 'bowel-screening',
      facilityId: FACILITY,
      stage: 'FollowUpActive',
      urgency: ROUTINE,
      owner: SARA,
      proposedOwner: ENDOSCOPY_UNIT,
      nextAction: 'respond-to-referral',
      dueAt: at('2026-11-13T12:00:00+04:00'),
      isEscalated: false,
      exceptions: [],
      tracks: [
        track('Notification', 'Understood', true, '2026-11-09T11:00:00+04:00'),
        track('Consultation', 'OutcomeRecorded', true, '2026-11-10T09:30:00+04:00'),
        track('Referral', 'Sent', false, '2026-11-11T10:00:00+04:00'),
        notStarted('Appointment'),
      ],
      outcomeEvidenceReference: null,
      closureReason: null,
      createdAt: at('2026-11-08T09:00:00+04:00'),
      updatedAt: at('2026-11-11T10:00:00+04:00'),
    },
    timeline: entries(caseId, [
      {
        type: 'ScreeningResultReceived',
        time: '2026-11-08T09:00:00+04:00',
        by: BY_SARA,
        from: null,
        to: 'ReviewPending',
        reason: 'Screening result needs a colonoscopy.',
        evidence: 'DiagnosticReport/fit-2026-110207',
      },
      {
        type: 'ClinicalReviewCompleted',
        time: '2026-11-08T11:00:00+04:00',
        by: BY_SARA,
        from: 'ReviewPending',
        to: 'FollowUpActive',
        reason: 'Reviewed as routine.',
      },
      {
        type: 'NotificationUpdated',
        time: '2026-11-09T11:00:00+04:00',
        by: BY_HUDA,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Next step understood: he said it back on a call.',
      },
      {
        type: 'ConsultationUpdated',
        time: '2026-11-10T09:30:00+04:00',
        by: BY_SARA,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Consultation outcome recorded: refer for colonoscopy.',
      },
      {
        type: 'ReferralUpdated',
        time: '2026-11-11T10:00:00+04:00',
        by: BY_SARA,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Referral sent to the Endoscopy Unit, General Hospital. The sender stays responsible until it is accepted.',
      },
    ]),
  }
}

function mohammed(): CaseFixture {
  const caseId = 'a5b7d9f1-7c6a-4f8b-8ca3-100538920000'
  return {
    detail: {
      caseId,
      sourceSystem: 'fhc-his',
      sourceResultId: 'LAB-2026-111102',
      patientReference: 'Patient/p-100538920',
      pathwayId: 'heart-kidney-diabetes',
      facilityId: FACILITY,
      stage: 'ReviewPending',
      urgency: null,
      owner: SARA,
      proposedOwner: null,
      nextAction: 'review-result',
      dueAt: at('2026-11-13T17:00:00+04:00'),
      isEscalated: false,
      exceptions: [],
      tracks: [notStarted('Notification'), notStarted('Consultation'), notStarted('Referral'), notStarted('Appointment')],
      outcomeEvidenceReference: null,
      closureReason: null,
      createdAt: at('2026-11-12T08:00:00+04:00'),
      updatedAt: at('2026-11-12T08:00:00+04:00'),
    },
    timeline: entries(caseId, [
      {
        type: 'ScreeningResultReceived',
        time: '2026-11-12T08:00:00+04:00',
        by: BY_SARA,
        from: null,
        to: 'ReviewPending',
        reason: 'Blood test result needs follow-up.',
        evidence: 'DiagnosticReport/lab-2026-111102',
      },
    ]),
  }
}

/**
 * Builds a fresh set of the eight worklist cases from board 1, each with its timeline.
 *
 * @returns New objects every call, so each server starts from the same state.
 */
export function createCases(): CaseFixture[] {
  return [hessa(), noura(), salem(), khalid(), maricel(), nasreen(), rashid(), mohammed()]
}

/**
 * Loop-closure numbers consistent with board 3, where the contract has a field.
 *
 * @returns The metrics, computed now.
 */
export function createMetrics(): Metrics {
  return {
    generatedAt: mockNow().toISOString(),
    totalCases: 240,
    openCases: 56,
    closedCases: 184,
    casesByStage: { Received: 1, ReviewPending: 3, FollowUpActive: 38, ClosureReview: 14 },
    casesByException: { Overdue: 9, Rejected: 4, Unreachable: 3, Unacknowledged: 5, Unassigned: 2 },
    overdueCases: 9,
    escalatedCases: 4,
    unassignedCases: 2,
    acknowledgementRate: 0.966,
    closureRate: 0.7667,
    medianTimeToClosure: '19.00:00:00',
  }
}

/** The seed of the shared `cohort` scenario. */
export const COHORT_SEED = 20260101

/** The arm of the comparison with reminders and escalation on: board 3's "With escalation". */
export const WITH_ESCALATION: SimulationArm = {
  escalationEnabled: true,
  totalCases: 240,
  closedCases: 184,
  stalledCases: 9,
  escalationsRaised: 31,
  medianTimeToClosure: '19.00:00:00',
}

/** The arm of the comparison with reminders and escalation off: board 3's "Escalation off". */
export const WITHOUT_ESCALATION: SimulationArm = {
  escalationEnabled: false,
  totalCases: 240,
  closedCases: 151,
  stalledCases: 38,
  escalationsRaised: 0,
  medianTimeToClosure: '31.00:00:00',
}

const HESSA_SECTIONS: HandoffSummary['sections'] = [
  {
    heading: 'Situation',
    text: 'Her screening mammogram on Mon 9 Nov 2026 needs further assessment.',
    sourceFactIds: ['screening-report-2026-11-10'],
  },
  {
    heading: 'Background',
    text: 'A first referral to Regional Hospital was declined on Tue 10 Nov (no capacity). She speaks Arabic. With her written consent, her son gets her appointment details.',
    sourceFactIds: ['referral-declined-2026-11-10', 'consent-form-2026-11-09'],
  },
  {
    heading: 'Assessment',
    text: 'See the screening report in the clinical record.',
    sourceFactIds: [],
  },
  {
    heading: 'Request',
    text: 'Please accept her and offer an assessment by Mon 16 Nov.',
    sourceFactIds: ['breast-screening-programme-v1'],
  },
]

/**
 * Runs sections together the way the API stores a summary's text: each heading on its own line,
 * then its text, with a blank line between sections.
 *
 * @param sections - The sections, in reading order.
 * @returns The plain text.
 */
export function flatten(sections: HandoffSummary['sections']): string {
  return sections.map((section) => `${section.heading}\n${section.text}`).join('\n\n')
}

/**
 * Drafts the handoff summary of board 18 for Hessa's case.
 *
 * @param summaryId - The identifier to give the draft.
 * @returns A new draft, waiting for a person to review it.
 */
export function draftHessaSummary(summaryId: string): HandoffSummary {
  return {
    summaryId,
    caseId: HESSA_CASE_ID,
    status: 'Draft',
    sections: HESSA_SECTIONS.map((section) => ({ ...section, sourceFactIds: [...section.sourceFactIds] })),
    text: flatten(HESSA_SECTIONS),
    missingOrConflictingFacts: [
      'Preferred appointment time: not in the facts. Add it if she told you.',
      'Help to get there: not recorded.',
    ],
    humanReviewRequired: true,
    draftedBy: 'AiWorkflow',
    modelDeployment: 'summary-drafter-2026-10',
    promptVersion: 'fr11-v1',
    trigger: 'Requested',
    language: 'en',
    draftedAt: mockNow().toISOString(),
    approvedBy: null,
    approvedAt: null,
    rejectedBy: null,
    rejectedAt: null,
    rejectionReason: null,
  }
}
