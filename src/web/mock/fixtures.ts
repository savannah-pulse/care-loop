/**
 * Fixtures for the local mock API: the eight worklist cases of UI design board 1 with the patients'
 * names and MRNs, Hessa's case, timeline and escalation from boards 2 and 17, a case escalated to the
 * programme coordinator, Hessa's earlier screening (board 18), two cases at the General Hospital,
 * the five programmes of board 4, loop-closure numbers from board 3 and the handoff summary draft
 * from board 18. References are written as the API stores them, without a FHIR type. Every person,
 * reference and number is invented. Typed against the generated API schema, so a contract change
 * that breaks them fails the type check.
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
/** A programme's settings as `GET /programmes` returns them. */
export type Programme = Schemas['ProgrammeResponse']
/** One arm of the escalation comparison. */
export type SimulationArm = Schemas['SimulationArmResponse']

type Owner = Schemas['OwnerResponse']
type Escalation = Schemas['EscalationResponse']
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
const GENERAL_HOSPITAL = 'general-hospital'

/** The hospital system the Family Health Centre's results come from. */
const HOSPITAL_SYSTEM = 'fhc-his'
/** The General Hospital's hospital system. */
const GENERAL_HOSPITAL_SYSTEM = 'gh-his'
/**
 * Who the API names on what comes back by text message or email (a delivery report or a reply):
 * the case's own source system, because no person acted.
 */
const MESSAGE_ACTOR = HOSPITAL_SYSTEM

const REKHA: Owner = { reference: 'rekha-varghese', role: 'clinician', facilityId: FACILITY, isQueue: false }
const HUDA: Owner = { reference: 'huda-saleh', role: 'nurse-navigator', facilityId: FACILITY, isQueue: false }
const SARA: Owner = { reference: 'sara-al-hammadi', role: 'clinician', facilityId: FACILITY, isQueue: false }
const LEAD_QUEUE: Owner = {
  reference: 'facility-lead:family-health-centre',
  role: 'facility-lead',
  facilityId: FACILITY,
  isQueue: true,
}
const ENDOSCOPY_UNIT: Owner = {
  reference: 'endoscopy-unit-general-hospital',
  role: 'receiving-clinician',
  facilityId: 'general-hospital',
  isQueue: false,
}

const LAYLA: Owner = { reference: 'layla-haddad', role: 'receiving-clinician', facilityId: GENERAL_HOSPITAL, isQueue: false }
const JOY = { actor: 'joy-bautista', role: 'facility-lead' } as const

const SYSTEM = { actor: 'careloop', role: 'system' } as const
const BY_REKHA = { actor: REKHA.reference, role: REKHA.role } as const
const BY_HUDA = { actor: HUDA.reference, role: HUDA.role } as const
const BY_SARA = { actor: SARA.reference, role: SARA.role } as const
const BY_LAYLA = { actor: LAYLA.reference, role: LAYLA.role } as const

interface EscalationSpec {
  readonly id: string
  readonly level: number
  readonly to: 'facility-lead' | 'coordinator'
  readonly raised: string
  readonly nextAction: string
  readonly due: string
  readonly facilityId?: string
  readonly acknowledged?: { readonly by: string; readonly role: string; readonly at: string }
}

function escalation(spec: EscalationSpec): Escalation {
  const facilityId = spec.facilityId ?? FACILITY
  return {
    escalationId: spec.id,
    level: spec.level,
    escalatedTo: { reference: `${spec.to}:${facilityId}`, role: spec.to, facilityId, isQueue: true },
    raisedAt: at(spec.raised),
    reason: `Next action '${spec.nextAction}' was due at ${at(spec.due)}.`,
    acknowledgedBy: spec.acknowledged?.by ?? null,
    acknowledgedByRole: spec.acknowledged?.role ?? null,
    acknowledgedAt: spec.acknowledged ? at(spec.acknowledged.at) : null,
    resolvedAt: null,
  }
}

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
  /** Where the entry came from, when the actor alone does not say: `sms` or `email` for a channel callback. */
  readonly origin?: string
}

/** Event types a person records in CareLoop itself rather than in the hospital system. */
const CARELOOP_EVENTS: ReadonlySet<EventType> = new Set<EventType>(['EscalationAcknowledged', 'NotificationUpdated', 'HandoffSummaryApproved'])

/** Where an entry came from, as the API's `origin` says it. */
function originOf(spec: EntrySpec): string {
  if (spec.origin) {
    return spec.origin
  }
  if (spec.by.actor === 'careloop' || CARELOOP_EVENTS.has(spec.type)) {
    return 'careloop'
  }
  return 'hospital'
}

function entries(caseId: string, specs: readonly EntrySpec[]): TimelineEntry[] {
  return specs.map((spec, index) => {
    const source = at(spec.time)
    return {
      auditEventId: `${caseId.slice(0, -3)}${String(index + 1).padStart(3, '0')}`,
      sourceSystem: spec.by.actor === 'careloop' ? 'careloop' : HOSPITAL_SYSTEM,
      eventType: spec.type,
      disposition: spec.refused ? 'Rejected' : 'Applied',
      actorReference: spec.by.actor,
      actorRole: spec.by.role,
      sourceTimestamp: source,
      receivedTimestamp: new Date(Date.parse(source) + 4000).toISOString(),
      priorStage: spec.from,
      newStage: spec.to,
      reason: spec.reason,
      origin: originOf(spec),
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
      patientReference: 'p-100245891',
      patientDisplayName: 'Hessa Al Dhaheri',
      patientSecondaryIdentifier: '100245891',
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
      displayLabel: 'FollowUp',
      escalations: [
        escalation({
          id: '5d1e2f3a-4b5c-4d6e-8f70-100245891e01',
          level: 1,
          to: 'facility-lead',
          raised: '2026-11-11T15:00:00+04:00',
          nextAction: 'arrange-alternative-referral',
          due: '2026-11-11T15:00:00+04:00',
        }),
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
        by: { actor: MESSAGE_ACTOR, role: 'system' },
        origin: 'sms',
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
        by: { actor: 'breast-clinic-regional-hospital', role: 'receiving-clinician' },
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
      patientReference: 'p-100318402',
      patientDisplayName: 'Noura Al Shamsi',
      patientSecondaryIdentifier: '100318402',
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
      displayLabel: 'Consultation',
      escalations: [],
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
      patientReference: 'p-100377615',
      patientDisplayName: 'Salem Al Ketbi',
      patientSecondaryIdentifier: '100377615',
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
      displayLabel: 'PatientAcknowledged',
      escalations: [],
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
        by: { actor: MESSAGE_ACTOR, role: 'system' },
        origin: 'sms',
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Replied 1 to the text message: he got the message.',
      },
      {
        type: 'NotificationUpdated',
        time: '2026-11-12T08:40:00+04:00',
        by: { actor: MESSAGE_ACTOR, role: 'system' },
        origin: 'sms',
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
      patientReference: 'p-100402233',
      patientDisplayName: 'Khalid Al Marzouqi',
      patientSecondaryIdentifier: '100402233',
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
      displayLabel: 'OwnerAssigned',
      escalations: [],
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
      patientReference: 'p-100455790',
      patientDisplayName: 'Maricel Reyes',
      patientSecondaryIdentifier: '100455790',
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
      displayLabel: 'PatientNotified',
      escalations: [],
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
      patientReference: 'p-100468104',
      patientDisplayName: 'Nasreen Akhtar',
      patientSecondaryIdentifier: '100468104',
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
      displayLabel: 'PatientNotified',
      escalations: [],
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
        by: { actor: MESSAGE_ACTOR, role: 'system' },
        origin: 'sms',
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
      patientReference: 'p-100512337',
      patientDisplayName: 'Rashid Al Hosani',
      patientSecondaryIdentifier: '100512337',
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
      displayLabel: 'FollowUp',
      escalations: [],
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
      patientReference: 'p-100538920',
      patientDisplayName: 'Mohammed Rafiq',
      patientSecondaryIdentifier: '100538920',
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
      displayLabel: 'OwnerAssigned',
      escalations: [],
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

function latifa(): CaseFixture {
  const caseId = 'c6d8e0f2-8d7b-4a9c-9db4-100587221000'
  return {
    detail: {
      caseId,
      sourceSystem: HOSPITAL_SYSTEM,
      sourceResultId: 'HPV-2026-110455',
      patientReference: 'p-100587221',
      patientDisplayName: 'Latifa Al Mazrouei',
      patientSecondaryIdentifier: '100587221',
      displayLabel: 'PatientNotified',
      escalations: [
        escalation({
          id: '6e2f3a4b-5c6d-4e7f-8a91-100587221e01',
          level: 1,
          to: 'facility-lead',
          raised: '2026-11-10T17:00:00+04:00',
          nextAction: 'contact-patient-another-way',
          due: '2026-11-10T17:00:00+04:00',
          acknowledged: { by: JOY.actor, role: JOY.role, at: '2026-11-11T08:10:00+04:00' },
        }),
        escalation({
          id: '6e2f3a4b-5c6d-4e7f-8a91-100587221e02',
          level: 2,
          to: 'coordinator',
          raised: '2026-11-11T17:00:00+04:00',
          nextAction: 'contact-patient-another-way',
          due: '2026-11-10T17:00:00+04:00',
        }),
      ],
      outcomeEvidenceKind: null,
      confidence: null,
      pathwayId: 'cervical-screening',
      facilityId: FACILITY,
      stage: 'FollowUpActive',
      urgency: ROUTINE,
      owner: HUDA,
      proposedOwner: null,
      nextAction: 'contact-patient-another-way',
      dueAt: at('2026-11-10T17:00:00+04:00'),
      isEscalated: true,
      exceptions: ['Overdue', 'Unreachable'],
      tracks: [
        track('Notification', 'Failed', false, '2026-11-09T16:00:00+04:00'),
        notStarted('Consultation'),
        notStarted('Referral'),
        notStarted('Appointment'),
      ],
      outcomeEvidenceReference: null,
      closureReason: null,
      createdAt: at('2026-11-08T10:00:00+04:00'),
      updatedAt: at('2026-11-11T17:00:00+04:00'),
    },
    timeline: entries(caseId, [
      {
        type: 'ScreeningResultReceived',
        time: '2026-11-08T10:00:00+04:00',
        by: BY_SARA,
        from: null,
        to: 'ReviewPending',
        reason: 'Screening result needs follow-up.',
        evidence: 'DiagnosticReport/hpv-2026-110455',
      },
      {
        type: 'ClinicalReviewCompleted',
        time: '2026-11-08T13:00:00+04:00',
        by: BY_SARA,
        from: 'ReviewPending',
        to: 'FollowUpActive',
        reason: 'Reviewed as routine.',
      },
      {
        type: 'NotificationUpdated',
        time: '2026-11-09T16:00:00+04:00',
        by: { actor: MESSAGE_ACTOR, role: 'system' },
        origin: 'sms',
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Text not delivered: the number is not in service.',
      },
      {
        type: 'DeadlineElapsed',
        time: '2026-11-10T11:00:00+04:00',
        by: SYSTEM,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Reminder sent to Huda Saleh: another way to reach her is due at 17:00.',
      },
      {
        type: 'DeadlineElapsed',
        time: '2026-11-10T17:00:00+04:00',
        by: SYSTEM,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'No other way to reach her was found by the deadline. Escalated to the facility lead.',
      },
      {
        type: 'EscalationAcknowledged',
        time: '2026-11-11T08:10:00+04:00',
        by: JOY,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Escalation acknowledged. The person responsible and the deadline stay the same.',
      },
      {
        type: 'DeadlineElapsed',
        time: '2026-11-11T17:00:00+04:00',
        by: SYSTEM,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Still late a day after the deadline. Escalated to the programme coordinator.',
      },
    ]),
  }
}

function hessaEarlier(): CaseFixture {
  const caseId = '2a4c6e80-9e8c-4b0d-8ec5-100245891025'
  return {
    detail: {
      caseId,
      sourceSystem: HOSPITAL_SYSTEM,
      sourceResultId: 'FIT-2025-030311',
      patientReference: 'p-100245891',
      patientDisplayName: 'Hessa Al Dhaheri',
      patientSecondaryIdentifier: '100245891',
      displayLabel: 'Closed',
      escalations: [],
      outcomeEvidenceKind: 'diagnosis',
      confidence: null,
      pathwayId: 'bowel-screening',
      facilityId: FACILITY,
      stage: 'Closed',
      urgency: ROUTINE,
      owner: SARA,
      proposedOwner: null,
      nextAction: null,
      dueAt: null,
      isEscalated: false,
      exceptions: [],
      tracks: [
        track('Notification', 'Understood', true, '2025-03-04T10:00:00+04:00'),
        track('Consultation', 'OutcomeRecorded', true, '2025-03-05T09:00:00+04:00'),
        track('Referral', 'Accepted', true, '2025-03-06T12:00:00+04:00'),
        track('Appointment', 'Attended', true, '2025-04-15T09:00:00+04:00'),
      ],
      outcomeEvidenceReference: 'Procedure/colonoscopy-2025-04-15',
      closureReason: 'NegativeOnConfirmation',
      createdAt: at('2025-03-03T09:00:00+04:00'),
      updatedAt: at('2025-04-24T11:00:00+04:00'),
    },
    timeline: entries(caseId, [
      {
        type: 'ScreeningResultReceived',
        time: '2025-03-03T09:00:00+04:00',
        by: BY_SARA,
        from: null,
        to: 'ReviewPending',
        reason: 'Screening result needs a colonoscopy.',
        evidence: 'DiagnosticReport/fit-2025-030311',
      },
      {
        type: 'ClosureDecided',
        time: '2025-04-24T11:00:00+04:00',
        by: BY_SARA,
        from: 'ClosureReview',
        to: 'Closed',
        reason: 'Closed: the colonoscopy found no cancer.',
        evidence: 'Procedure/colonoscopy-2025-04-15',
      },
    ]),
  }
}

function shamma(): CaseFixture {
  const caseId = 'd7e9f1a3-9e8c-4cad-8ec5-100611045000'
  return {
    detail: {
      caseId,
      sourceSystem: GENERAL_HOSPITAL_SYSTEM,
      sourceResultId: 'MAMMO-2026-210118',
      patientReference: 'p-100611045',
      patientDisplayName: 'Shamma Al Kaabi',
      patientSecondaryIdentifier: '100611045',
      displayLabel: 'FollowUp',
      escalations: [],
      outcomeEvidenceKind: null,
      confidence: 5,
      pathwayId: 'breast-screening',
      facilityId: GENERAL_HOSPITAL,
      stage: 'FollowUpActive',
      urgency: ROUTINE,
      owner: LAYLA,
      proposedOwner: null,
      nextAction: 'rebook-appointment',
      dueAt: at('2026-11-13T12:00:00+04:00'),
      isEscalated: false,
      exceptions: [],
      tracks: [
        track('Notification', 'Understood', true, '2026-11-09T10:00:00+04:00'),
        notStarted('Consultation'),
        track('Referral', 'Accepted', false, '2026-11-09T15:00:00+04:00'),
        track('Appointment', 'Missed', false, '2026-11-12T08:00:00+04:00'),
      ],
      outcomeEvidenceReference: null,
      closureReason: null,
      createdAt: at('2026-11-08T09:00:00+04:00'),
      updatedAt: at('2026-11-12T08:00:00+04:00'),
    },
    timeline: entries(caseId, [
      {
        type: 'ScreeningResultReceived',
        time: '2026-11-08T09:00:00+04:00',
        by: BY_LAYLA,
        from: null,
        to: 'ReviewPending',
        reason: 'Screening result needs further assessment.',
        evidence: 'DiagnosticReport/mammo-2026-210118',
      },
      {
        type: 'AppointmentUpdated',
        time: '2026-11-12T08:00:00+04:00',
        by: BY_LAYLA,
        from: 'FollowUpActive',
        to: 'FollowUpActive',
        reason: 'Assessment appointment missed.',
      },
    ]),
  }
}

function mariam(): CaseFixture {
  const caseId = 'e8f0a2b4-af9d-4dbe-9fd6-100624310000'
  return {
    detail: {
      caseId,
      sourceSystem: GENERAL_HOSPITAL_SYSTEM,
      sourceResultId: 'LDCT-2026-210307',
      patientReference: 'p-100624310',
      patientDisplayName: 'Mariam Al Mansoori',
      patientSecondaryIdentifier: '100624310',
      displayLabel: 'OwnerAssigned',
      escalations: [],
      outcomeEvidenceKind: null,
      confidence: null,
      pathwayId: 'lung-screening',
      facilityId: GENERAL_HOSPITAL,
      stage: 'ReviewPending',
      urgency: null,
      owner: LAYLA,
      proposedOwner: null,
      nextAction: 'review-result',
      dueAt: at('2026-11-12T17:00:00+04:00'),
      isEscalated: false,
      exceptions: [],
      tracks: [notStarted('Notification'), notStarted('Consultation'), notStarted('Referral'), notStarted('Appointment')],
      outcomeEvidenceReference: null,
      closureReason: null,
      createdAt: at('2026-11-12T07:00:00+04:00'),
      updatedAt: at('2026-11-12T07:00:00+04:00'),
    },
    timeline: entries(caseId, [
      {
        type: 'ScreeningResultReceived',
        time: '2026-11-12T07:00:00+04:00',
        by: BY_LAYLA,
        from: null,
        to: 'ReviewPending',
        reason: 'Screening result needs review.',
        evidence: 'DiagnosticReport/ldct-2026-210307',
      },
    ]),
  }
}

/**
 * Builds a fresh set of cases, each with its timeline: the eight worklist cases of board 1, a ninth
 * case at the Family Health Centre escalated to the programme coordinator, Hessa's earlier, closed
 * bowel screening (her other screening on board 18), and two cases at the General Hospital.
 *
 * @returns New objects every call, so each server starts from the same state.
 */
export function createCases(): CaseFixture[] {
  return [
    hessa(),
    noura(),
    salem(),
    khalid(),
    maricel(),
    nasreen(),
    rashid(),
    mohammed(),
    latifa(),
    hessaEarlier(),
    shamma(),
    mariam(),
  ]
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
    measures: {
      acknowledgement: { numerator: 228, denominator: 236, rate: 228 / 236 },
      closure: { numerator: 184, denominator: 240, rate: 184 / 240 },
      understanding: { numerator: 208, denominator: 228, rate: 208 / 228 },
      nextStepOnTime: { numerator: 172, denominator: 200, rate: 172 / 200 },
      referralAcceptedAndAttended: { numerator: 201, denominator: 214, rate: 201 / 214 },
      appointmentsAttended: { numerator: 412, denominator: 429, rate: 412 / 429 },
      treatmentStartedOnTime: { numerator: 22, denominator: 25, rate: 22 / 25 },
      signedEvidence: { numerator: 184, denominator: 184, rate: 1 },
    },
    timeToStep: {
      acknowledgement: { denominator: 228, median: '12:00:00' },
      consultation: { denominator: 221, median: '1.00:00:00' },
      appointment: { denominator: 214, median: '5.00:00:00' },
      careStarted: { denominator: 25, median: '19.00:00:00' },
      closure: { denominator: 184, median: '19.00:00:00' },
    },
    closuresByReason: {
      Treated: 61,
      OnSurveillance: 40,
      NegativeOnConfirmation: 75,
      Declined: 4,
      TransferredAndAccepted: 3,
      Untraceable: 1,
    },
    casesReachingDisplayLabel: {
      ResultReceived: { numerator: 240, denominator: 240, rate: 1 },
      CaseOpened: { numerator: 240, denominator: 240, rate: 1 },
      OwnerAssigned: { numerator: 240, denominator: 240, rate: 1 },
      PatientNotified: { numerator: 236, denominator: 240, rate: 236 / 240 },
      PatientAcknowledged: { numerator: 228, denominator: 240, rate: 228 / 240 },
      Consultation: { numerator: 221, denominator: 240, rate: 221 / 240 },
      FollowUp: { numerator: 214, denominator: 240, rate: 214 / 240 },
      OutcomeRecorded: { numerator: 198, denominator: 240, rate: 198 / 240 },
      ClosureReview: { numerator: 190, denominator: 240, rate: 190 / 240 },
      Closed: { numerator: 184, denominator: 240, rate: 184 / 240 },
    },
    casesByDisplayLabel: {
      ResultReceived: 0,
      CaseOpened: 1,
      OwnerAssigned: 3,
      PatientNotified: 8,
      PatientAcknowledged: 7,
      Consultation: 7,
      FollowUp: 16,
      OutcomeRecorded: 8,
      ClosureReview: 6,
      Closed: 184,
    },
    confidenceRate: 196 / 236,
    confidenceDenominator: 236,
    confidenceResponses: 196,
    averageConfidence: 4.4,
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

const PLACEHOLDER_CITATION = 'PLACEHOLDER - no clinical source yet; to be cited by the clinical adviser'

type Deadline = Programme['deadlines'][number]

function deadline(nextAction: string, duration: string, warnBefore: string, ownerRole: string, urgencyCode: string | null = null): Deadline {
  return { nextAction, urgencyCode, duration, warnBefore, clinicallyApproved: false, ownerRole, citation: PLACEHOLDER_CITATION }
}

function programme(pathwayId: string, displayName: string): Programme {
  return {
    pathwayId,
    version: '0.0.1-placeholder',
    displayName,
    isPlaceholder: true,
    urgencyCodes: [ROUTINE, URGENT],
    unassignedQueueRole: 'facility-lead',
    stages: [
      { stage: 'Received', leavesOn: ['ScreeningResultReceived'], next: 'ReviewPending', allowedRoles: ['system'], nextActionOnEntry: 'assign-owner' },
      { stage: 'ReviewPending', leavesOn: ['ClinicalReviewCompleted'], next: 'FollowUpActive', allowedRoles: ['clinician'], nextActionOnEntry: 'review-result' },
      { stage: 'FollowUpActive', leavesOn: ['OutcomeRecorded'], next: 'ClosureReview', allowedRoles: ['clinician', 'receiving-clinician', 'lab'], nextActionOnEntry: 'acknowledge-notification' },
      { stage: 'ClosureReview', leavesOn: ['ClosureDecided'], next: 'Closed', allowedRoles: ['clinician', 'receiving-clinician'], nextActionOnEntry: 'decide-closure' },
      { stage: 'Closed', leavesOn: [], next: null, allowedRoles: [], nextActionOnEntry: null },
    ],
    tracks: [
      { kind: 'Notification', requiredForClosure: true, nextActionOnException: 'contact-patient-another-way' },
      { kind: 'Consultation', requiredForClosure: false, nextActionOnException: 'reassign-consultation' },
      { kind: 'Referral', requiredForClosure: false, nextActionOnException: 'arrange-alternative-referral' },
      { kind: 'Appointment', requiredForClosure: true, nextActionOnException: 'rebook-appointment' },
    ],
    deadlines: [
      deadline('assign-owner', '1.00:00:00', '06:00:00', 'facility-lead'),
      deadline('review-result', '1.00:00:00', '06:00:00', 'clinician'),
      deadline('acknowledge-notification', '1.00:00:00', '06:00:00', 'nurse-navigator'),
      deadline('answer-consultation', '1.00:00:00', '06:00:00', 'receiving-clinician'),
      deadline('respond-to-referral', '04:00:00', '01:00:00', 'receiving-clinician'),
      deadline('arrange-follow-up', '1.00:00:00', '06:00:00', 'clinician'),
      deadline('call-patient', '02:00:00', '00:30:00', 'nurse-navigator'),
      deadline('contact-patient-another-way', '1.00:00:00', '06:00:00', 'nurse-navigator'),
      deadline('reassign-consultation', '1.00:00:00', '06:00:00', 'clinician'),
      deadline('arrange-alternative-referral', '1.00:00:00', '04:00:00', 'clinician'),
      deadline('rebook-appointment', '1.00:00:00', '06:00:00', 'receiving-clinician'),
      deadline('start-treatment', '1.00:00:00', '06:00:00', 'receiving-clinician'),
      deadline('decide-closure', '1.00:00:00', '06:00:00', 'receiving-clinician'),
    ],
    escalationLadder: [
      { level: 1, escalateToRole: 'facility-lead', afterOverdue: '00:00:00', clinicallyApproved: false },
      { level: 2, escalateToRole: 'coordinator', afterOverdue: '1.00:00:00', clinicallyApproved: false },
    ],
    closureRules: [
      { reason: 'Treated', authorizedRoles: ['clinician', 'receiving-clinician'], isException: false, requiresEvidenceReference: true, requiresExceptionNote: false, requiresNextSurveillanceDate: false, minimumDocumentedAttempts: 0, citation: PLACEHOLDER_CITATION },
      { reason: 'OnSurveillance', authorizedRoles: ['clinician', 'receiving-clinician'], isException: false, requiresEvidenceReference: true, requiresExceptionNote: false, requiresNextSurveillanceDate: true, minimumDocumentedAttempts: 0, citation: PLACEHOLDER_CITATION },
      { reason: 'NegativeOnConfirmation', authorizedRoles: ['clinician', 'receiving-clinician'], isException: false, requiresEvidenceReference: true, requiresExceptionNote: false, requiresNextSurveillanceDate: false, minimumDocumentedAttempts: 0, citation: PLACEHOLDER_CITATION },
      { reason: 'Declined', authorizedRoles: ['clinician', 'receiving-clinician'], isException: true, requiresEvidenceReference: false, requiresExceptionNote: true, requiresNextSurveillanceDate: false, minimumDocumentedAttempts: 0, citation: PLACEHOLDER_CITATION },
      { reason: 'TransferredAndAccepted', authorizedRoles: ['clinician', 'receiving-clinician'], isException: true, requiresEvidenceReference: true, requiresExceptionNote: true, requiresNextSurveillanceDate: false, minimumDocumentedAttempts: 0, citation: PLACEHOLDER_CITATION },
      { reason: 'Untraceable', authorizedRoles: ['clinician', 'receiving-clinician'], isException: true, requiresEvidenceReference: false, requiresExceptionNote: true, requiresNextSurveillanceDate: false, minimumDocumentedAttempts: 1, citation: PLACEHOLDER_CITATION },
    ],
    evidenceKinds: [
      { kind: 'diagnosis', description: 'PLACEHOLDER - a confirmed diagnosis was documented at the source', clinicallyApproved: false, nextAction: null },
      { kind: 'treatment-plan', description: 'PLACEHOLDER - a treatment plan was documented; treatment must then start by the start-treatment deadline', clinicallyApproved: false, nextAction: 'start-treatment' },
      { kind: 'treatment-started', description: 'PLACEHOLDER - treatment started; the case returns to closure review', clinicallyApproved: false, nextAction: 'decide-closure' },
      { kind: 'surveillance-started', description: 'PLACEHOLDER - the patient was placed on surveillance; the case returns to closure review', clinicallyApproved: false, nextAction: 'decide-closure' },
    ],
    changeLog: [
      { version: '0.0.0-placeholder', date: '2026-11-13', approver: 'PLACEHOLDER - not clinically approved; drafted by the engineering team', summary: 'Placeholder definition for the prototype.' },
      { version: '0.0.1-placeholder', date: '2026-11-13', approver: 'PLACEHOLDER - not clinically approved; drafted by the engineering team', summary: 'Reply deadlines for consultations, referrals and call-backs, and the kinds of outcome evidence.' },
    ],
  }
}

/**
 * The five screening programmes of board 4, as \`GET /programmes\` returns them.
 *
 * @returns New objects every call. Every value is a placeholder waiting for a clinical adviser, as in
 * the API's own pathway files.
 */
export function createProgrammes(): Programme[] {
  return [
    programme('bowel-screening', 'PLACEHOLDER - Bowel screening follow-up'),
    programme('breast-screening', 'PLACEHOLDER - Breast screening follow-up'),
    programme('cervical-screening', 'PLACEHOLDER - Cervical screening follow-up'),
    programme('heart-kidney-diabetes', 'PLACEHOLDER - Heart, kidney and diabetes check follow-up'),
    programme('lung-screening', 'PLACEHOLDER - Lung screening follow-up'),
  ].map((item) =>
    item.pathwayId === 'breast-screening'
      ? {
          ...item,
          deadlines: [
            ...item.deadlines.slice(0, 3),
            deadline('acknowledge-notification', '1.00:00:00', '06:00:00', 'nurse-navigator', ROUTINE),
            ...item.deadlines.slice(3),
          ],
        }
      : item,
  )
}
/**
 * What the role-briefing agent writes in the mock: one sentence for the "Start here today" card.
 *
 * @param coordinator - `true` for the programme coordinator, whose list holds only cases escalated to them.
 * @returns A synthetic sentence. It names no patient: the API never gives an agent a name.
 */
export function roleBriefingWords(coordinator: boolean): string {
  return coordinator
    ? 'Two cases are escalated to you and both are past their deadline. Start with the one that has waited longest, then check whether the facility lead has acknowledged the other.'
    : 'One case is late and already escalated, so start there. The urgent one is due at noon, and the last one by the end of the day.'
}

/** What the impact-summary agent writes in the mock: a few sentences on the numbers, with no numbers in them. */
export const IMPACT_SUMMARY_WORDS = [
  'Most next steps are being done by their deadline, and most patients who were called said their next step back.',
  'Where cases are waiting, it is mostly for a unit to accept a referral, often after another unit declined for lack of capacity.',
  'A few appointments whose time has passed have no outcome recorded yet, which is worth checking before reading the attendance figure.',
].join('\n\n')

/**
 * What the patient-notification agent writes in the mock: a preview of a message to a patient.
 *
 * @param channel - `Sms` for a text message, `Email` for an email.
 * @param facility - The care team's facility, by name.
 * @returns A synthetic draft with `{first name}` where the name goes and the two reply codes at the end. It never
 * mentions a result.
 */
export function patientMessageWords(channel: 'Sms' | 'Email', facility: string): string {
  return channel === 'Sms'
    ? `{first name}, this is your care team at ${facility}. We have a follow-up step for you and will be in touch about it soon. Reply 1 to confirm you got this message. Reply 2 if you would like your care team to call you.`
    : [
        'Subject: A follow-up step from your care team',
        `Dear {first name},\n\nThis is your care team at ${facility}. We have a follow-up step for you, and someone from the team will be in touch about it soon.`,
        'Reply 1 to confirm you got this message. Reply 2 if you would like your care team to call you.',
        `Your care team at ${facility}`,
      ].join('\n\n')
}
