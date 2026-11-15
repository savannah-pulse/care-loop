import { describe, expect, it } from 'vitest'
import { TEST_NOW, caseDetail, escalation, leadQueue, programme, timelineEntry } from '../testing/builders'
import {
  canAcknowledge,
  currentEscalation,
  deadlineFor,
  escalationLadder,
  escalationNotices,
  escalationTarget,
  isAddressedTo,
  noticeActor,
} from './escalations'

const JOY = { role: 'facility-lead', facilityId: 'family-health-centre' }
const LEAD_ELSEWHERE = { role: 'facility-lead', facilityId: 'general-hospital' }
const REKHA = { role: 'clinician', facilityId: 'family-health-centre' }
const OBAID = { role: 'coordinator' }

const toCoordinator = escalation({
  escalationId: 'escalation-2',
  level: 2,
  escalatedTo: { reference: 'coordinator', role: 'coordinator', facilityId: 'family-health-centre', isQueue: true },
  raisedAt: '2026-11-12T11:00:00Z',
})

const acknowledgedByJoy = escalation({
  acknowledgedBy: 'Practitioner/joy-bautista',
  acknowledgedByRole: 'facility-lead',
  acknowledgedAt: '2026-11-11T12:00:00Z',
})

describe('who may acknowledge (C15)', () => {
  it('offers the acknowledgement to the role the escalation went to, at the case facility', () => {
    const detail = caseDetail({ escalations: [escalation()] })
    expect(canAcknowledge(detail, JOY)).toBe(true)
    expect(canAcknowledge(detail, REKHA)).toBe(false)
    expect(canAcknowledge(detail, OBAID)).toBe(false)
  })

  it('never offers it to a facility lead from another facility, although the API would accept a reason', () => {
    expect(canAcknowledge(caseDetail({ escalations: [escalation()] }), LEAD_ELSEWHERE)).toBe(false)
  })

  it('offers only the latest open escalation, once', () => {
    const climbed = caseDetail({ escalations: [acknowledgedByJoy, toCoordinator] })
    expect(currentEscalation(climbed.escalations)).toBe(toCoordinator)
    expect(canAcknowledge(climbed, JOY)).toBe(false)
    expect(canAcknowledge(climbed, OBAID)).toBe(true)
    expect(canAcknowledge(caseDetail({ escalations: [acknowledgedByJoy] }), JOY)).toBe(false)
    expect(canAcknowledge(caseDetail({ escalations: [escalation({ resolvedAt: '2026-11-11T13:00:00Z' })] }), JOY)).toBe(false)
    expect(canAcknowledge(caseDetail({ escalations: [] }), JOY)).toBe(false)
    expect(canAcknowledge(caseDetail({ stage: 'Closed', escalations: [escalation()] }), JOY)).toBe(false)
  })

  it('addresses a facility lead at their own facility, and the coordinator anywhere', () => {
    expect(isAddressedTo(leadQueue(), JOY)).toBe(true)
    expect(isAddressedTo(leadQueue(), LEAD_ELSEWHERE)).toBe(false)
    expect(isAddressedTo(toCoordinator.escalatedTo, OBAID)).toBe(true)
    expect(isAddressedTo({ role: 'Facility-Lead', facilityId: 'family-health-centre' }, JOY)).toBe(true)
  })

  it('names who an escalation went to by role and facility, never by a person', () => {
    expect(escalationTarget(leadQueue()).name).toBe('Facility lead, Family Health Centre')
    expect(escalationTarget(toCoordinator.escalatedTo).inSentence).toBe('the programme coordinator')
  })
})

describe('escalation ladder (screen 17)', () => {
  const late = caseDetail({ dueAt: '2026-11-11T11:00:00Z', escalations: [escalation()] })

  it('lays out the reminder, then each level of the programme ladder, with where the case is now', () => {
    const rungs = escalationLadder({ detail: late, programme: programme(), now: TEST_NOW })
    expect(rungs.map((rung) => [rung.key, rung.state, rung.at?.toISOString(), rung.target.role])).toEqual([
      ['reminder', 'done', '2026-11-11T05:00:00.000Z', 'clinician'],
      ['level-1', 'current', '2026-11-11T11:00:00.000Z', 'facility-lead'],
      ['level-2', 'upcoming', '2026-11-12T11:00:00.000Z', 'coordinator'],
    ])
    expect(rungs[1].escalation).toBe(late.escalations[0])
    expect(rungs.every((rung) => rung.approved === false)).toBe(true)
  })

  it('says the reminder was sent only when the timeline records one', () => {
    expect(escalationLadder({ detail: late, programme: programme(), now: TEST_NOW })[0].sent).toBe(false)
    const timeline = [
      timelineEntry({ auditEventId: 'warning', sourceTimestamp: '2026-11-11T05:00:00Z' }),
      timelineEntry({ auditEventId: 'raise', sourceTimestamp: '2026-11-11T11:00:00Z', reason: 'Overdue deadline checked' }),
    ]
    const reminder = escalationLadder({ detail: late, programme: programme(), timeline, now: TEST_NOW })[0]
    expect(reminder.sent).toBe(true)
    expect(reminder.at?.toISOString()).toBe('2026-11-11T05:00:00.000Z')
  })

  it('shows every rung as still to come before the reminder time', () => {
    const onTime = caseDetail({ dueAt: '2026-11-11T11:00:00Z', escalations: [] })
    const rungs = escalationLadder({ detail: onTime, programme: programme(), now: new Date('2026-11-11T04:00:00Z') })
    expect(rungs.map((rung) => rung.state)).toEqual(['upcoming', 'upcoming', 'upcoming'])
    expect(rungs[1].at?.toISOString()).toBe('2026-11-11T11:00:00.000Z')
  })

  it('still shows an open escalation without the programme settings', () => {
    const rungs = escalationLadder({ detail: late, now: TEST_NOW })
    expect(rungs.map((rung) => [rung.key, rung.state])).toEqual([
      ['reminder', 'done'],
      ['level-1', 'current'],
    ])
  })

  it('finds the deadline for the urgency first, then the one for every urgency', () => {
    const settings = programme({
      deadlines: [
        { ...programme().deadlines[0], urgencyCode: 'PLACEHOLDER-URGENCY-B', duration: '04:00:00' },
        programme().deadlines[0],
      ],
    })
    expect(deadlineFor(settings, 'arrange-follow-up', 'PLACEHOLDER-URGENCY-B')?.duration).toBe('04:00:00')
    expect(deadlineFor(settings, 'arrange-follow-up', 'PLACEHOLDER-URGENCY-A')?.duration).toBe('1.00:00:00')
    expect(deadlineFor(settings, 'call-patient', null)).toBeUndefined()
    expect(deadlineFor(undefined, 'arrange-follow-up', null)).toBeUndefined()
  })
})

describe('notice history', () => {
  it('lists reminders, escalations and acknowledgements newest first, with refused attempts kept apart', () => {
    const timeline = [
      timelineEntry({ auditEventId: 'warning', sourceTimestamp: '2026-11-11T05:00:00Z' }),
      timelineEntry({ auditEventId: 'raise', sourceTimestamp: '2026-11-11T11:00:00Z' }),
      timelineEntry({
        auditEventId: 'refused',
        eventType: 'EscalationAcknowledged',
        disposition: 'Rejected',
        actorReference: 'obaid-al-suwaidi',
        actorRole: 'coordinator',
        sourceTimestamp: '2026-11-11T11:30:00Z',
        reason: 'Only the role addressed by this escalation may acknowledge it.',
      }),
      timelineEntry({
        auditEventId: 'applied',
        eventType: 'EscalationAcknowledged',
        actorReference: 'Practitioner/joy-bautista',
        actorRole: 'facility-lead',
        sourceTimestamp: '2026-11-11T12:00:00Z',
        reason: 'Escalation acknowledged; deadline and owner unchanged',
      }),
    ]
    const notices = escalationNotices([acknowledgedByJoy], timeline)
    expect(notices.map((notice) => [notice.kind, notice.at.toISOString()])).toEqual([
      ['acknowledged', '2026-11-11T12:00:00.000Z'],
      ['refused', '2026-11-11T11:30:00.000Z'],
      ['raised', '2026-11-11T11:00:00.000Z'],
      ['reminder', '2026-11-11T05:00:00.000Z'],
    ])
    expect(notices[1].actor).toEqual({ reference: 'obaid-al-suwaidi', role: 'coordinator' })
    expect(notices[1].reason).toBeUndefined()
  })

  it('recognises the signed-in person whichever form of reference the API kept', () => {
    expect(noticeActor({ reference: 'Practitioner/joy-bautista', role: 'facility-lead' }, 'joy-bautista')).toBe('you')
    expect(noticeActor({ reference: 'rekha-varghese', role: 'clinician' }, 'joy-bautista')).toBe('Dr. Rekha Varghese')
    expect(noticeActor({ reference: 'facility-lead-7', role: 'facility-lead' }, 'joy-bautista')).toBe('a facility lead')
  })
})
