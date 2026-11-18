import { describe, expect, it } from 'vitest'
import { caseDetail, caseSummary, escalation, timelineEntry } from '../testing/builders'
import { latestCase } from './changes'
import { caseLinkNames, caseNumber, describePatient } from './patients'
import { DISPLAY_LABELS, displayLabelText, displayStepIndex, isDisplayLabel } from './stages'
import { hasOwnReason, sourceLabel, timelineEntryTitle, timelineSource } from './timeline'

const NBSP = '\u00a0'
const STAFF_ROLE = 'nurse-navigator'

describe('patients (C20)', () => {
  it('shows the name with the MRN as the second identifier', () => {
    expect(describePatient(caseSummary(), 'nurse-navigator')).toMatchObject({
      name: 'Hessa Al Dhaheri',
      identifier: `MRN 100${NBSP}245${NBSP}891`,
      hidden: false,
      reference: 'p-100245891',
    })
  })

  it('falls back to a friendly patient number when the API sends no name', () => {
    const view = describePatient(caseSummary({ patientDisplayName: null, patientSecondaryIdentifier: null }), 'clinician')
    expect(view.name).toBe(`Patient 100${NBSP}245${NBSP}891`)
    expect(view.identifier).toBeUndefined()
  })

  it('hides names, MRNs and references from the programme coordinator and says so plainly', () => {
    const redacted = describePatient(caseSummary({ patientDisplayName: null, patientSecondaryIdentifier: null }), 'coordinator')
    expect(redacted).toEqual({
      name: 'Case 3F6B1C2A',
      identifier: 'Name hidden for your role',
      hidden: true,
      searchText: '3F6B1C2A',
    })
    expect(describePatient(caseSummary(), 'coordinator')).toEqual(redacted)
  })

  it('shortens a case identifier to a number people can read out', () => {
    expect(caseNumber('3f6b1c2a-8d4e-4f5a-9b7c-100245891000')).toBe('3F6B1C2A')
  })
})

describe('display stages (decision 19)', () => {
  it('shows the label the API sends, in plain words and in order', () => {
    expect(DISPLAY_LABELS).toHaveLength(10)
    expect(displayLabelText('FollowUp')).toBe('Follow-up')
    expect(displayLabelText('OwnerAssigned')).toBe('Owner assigned')
    expect(displayStepIndex('FollowUp')).toBe(6)
    expect(isDisplayLabel('ClosureReview')).toBe(true)
    expect(isDisplayLabel('FollowUpActive')).toBe(false)
  })
})

describe('timeline (C3)', () => {
  it('names where each entry came from, as the API reports it', () => {
    expect(sourceLabel(timelineSource(timelineEntry({ origin: 'careloop' })))).toBe('CareLoop')
    expect(sourceLabel(timelineSource(timelineEntry({ origin: 'sms', eventType: 'NotificationUpdated' })))).toBe('Text message')
    expect(sourceLabel(timelineSource(timelineEntry({ origin: 'email', eventType: 'NotificationUpdated' })))).toBe('Email')
    expect(sourceLabel(timelineSource(timelineEntry({ origin: 'hospital', sourceSystem: 'his-simulator' })))).toBe('Hospital system')
  })

  it('shows what a nurse recorded after a call as recorded in CareLoop, whatever system the case came from', () => {
    const recorded = timelineEntry({
      sourceSystem: 'fhc-his',
      origin: 'careloop',
      eventType: 'NotificationUpdated',
      actorReference: 'huda-saleh',
      actorRole: 'nurse-navigator',
    })
    expect(sourceLabel(timelineSource(recorded))).toBe('CareLoop')
  })

  it('reads an origin it does not know as a hospital system, never as a patient app or portal', () => {
    for (const origin of ['patient-channel', 'health-app', 'portal']) {
      expect(sourceLabel(timelineSource(timelineEntry({ origin })))).toBe('Hospital system')
    }
  })

  it('shows the programme coordinator CareLoop’s own wording, never what came back by text message or email', () => {
    const contact = (origin: string, reason: string) => timelineEntry({ origin, eventType: 'NotificationUpdated', reason })
    const call = contact('careloop', 'On a call, the patient said they will come.')
    expect(timelineEntryTitle(call, 'coordinator')).toBe('On a call, the patient said they will come.')
    for (const origin of ['sms', 'email']) {
      const reply = contact(origin, 'The patient replied 1: they got the message.')
      expect(timelineEntryTitle(reply, 'coordinator')).toBe('Patient contact updated')
      expect(timelineEntryTitle(reply, STAFF_ROLE)).toBe('The patient replied 1: they got the message.')
    }
  })

  it('never shows a refused acknowledgement as an acknowledgement', () => {
    const refused = timelineEntry({
      eventType: 'EscalationAcknowledged',
      disposition: 'Rejected',
      actorReference: 'obaid-al-suwaidi',
      actorRole: 'coordinator',
      reason: 'Only the role addressed by this escalation may acknowledge it.',
    })
    expect(timelineEntryTitle(refused, STAFF_ROLE)).toBe('Acknowledgement refused')
    expect(timelineEntryTitle({ ...refused, disposition: 'Applied', reason: 'EscalationAcknowledged applied' }, STAFF_ROLE)).toBe(
      'Escalation acknowledged',
    )
    expect(timelineEntryTitle(timelineEntry({ eventType: 'ReferralUpdated', disposition: 'Rejected', reason: '' }), STAFF_ROLE)).toBe(
      'Referral update not accepted',
    )
  })

  it('uses the reason only when it says more than the kind of event, and invents nothing', () => {
    const bare = timelineEntry({ eventType: 'ReferralUpdated', reason: 'ReferralUpdated applied' })
    expect(hasOwnReason(bare)).toBe(false)
    expect(timelineEntryTitle(bare, STAFF_ROLE)).toBe('Referral updated')
    expect(
      timelineEntryTitle(timelineEntry({ reason: 'Overdue deadline checked; exception flags or escalation steps raised' }), STAFF_ROLE),
    ).toBe('Deadline reached')
    expect(timelineEntryTitle(timelineEntry({ reason: 'Deadline warning raised' }), STAFF_ROLE)).toBe('Deadline warning raised')
  })
})

describe('case link names', () => {
  const subject = (caseId: string, programme: string, role: string = STAFF_ROLE) => ({
    caseId,
    patient: describePatient(caseSummary({ caseId }), role),
    programme,
  })

  it('adds the programme, then the case number, only where two links would still read the same', () => {
    expect(
      caseLinkNames([
        subject('1a2b3c4d-0000-4000-8000-000000000001', 'Breast screening'),
        subject('2b3c4d5e-0000-4000-8000-000000000002', 'Breast screening'),
        subject('3c4d5e6f-0000-4000-8000-000000000003', 'Bowel screening'),
      ]),
    ).toEqual([
      `Hessa Al Dhaheri, MRN 100${NBSP}245${NBSP}891, Breast screening, Case 1A2B3C4D`,
      `Hessa Al Dhaheri, MRN 100${NBSP}245${NBSP}891, Breast screening, Case 2B3C4D5E`,
      `Hessa Al Dhaheri, MRN 100${NBSP}245${NBSP}891, Bowel screening`,
    ])
  })

  it('names the coordinator’s links by case number alone, with no MRN even if the API sent one', () => {
    expect(caseLinkNames([subject('1a2b3c4d-0000-4000-8000-000000000001', 'Breast screening', 'coordinator')])).toEqual([
      'Case 1A2B3C4D',
    ])
  })
})

describe('a case the person just changed', () => {
  const before = caseDetail({ isEscalated: true, escalations: [escalation()], updatedAt: '2026-11-12T04:00:00Z' })
  const acknowledged = caseDetail({
    isEscalated: true,
    escalations: [escalation({ acknowledgedBy: 'joy-bautista', acknowledgedByRole: 'facility-lead', acknowledgedAt: '2026-11-12T05:00:00Z' })],
    updatedAt: '2026-11-12T05:00:00Z',
  })
  const change = { base: before, detail: acknowledged }

  it('shows what the API returned after the change until a newer read arrives', () => {
    expect(latestCase(before, undefined)).toBe(before)
    expect(latestCase(before, change)).toBe(acknowledged)
    const reread = { ...acknowledged }
    expect(latestCase(reread, change)).toBe(reread)
    const newer = caseDetail({ nextAction: 'decide-closure', updatedAt: '2026-11-12T05:30:00Z' })
    expect(latestCase(newer, change)).toBe(newer)
  })

  it('never lets a read that was on its way during the change undo it on screen', () => {
    expect(latestCase({ ...before }, change)).toBe(acknowledged)
  })
})
