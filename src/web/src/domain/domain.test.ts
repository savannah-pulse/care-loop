import { describe, expect, it } from 'vitest'
import { caseSummary, timelineEntry } from '../testing/builders'
import { caseNumber, describePatient } from './patients'
import { DISPLAY_LABELS, displayLabelText, displayStepIndex, isDisplayLabel } from './stages'
import { hasOwnReason, sourceLabel, timelineEntryTitle, timelineSource } from './timeline'

const NBSP = '\u00a0'

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
  it('names where each entry came from in plain words', () => {
    expect(sourceLabel(timelineSource(timelineEntry({ sourceSystem: 'careloop' })))).toBe('CareLoop')
    expect(
      sourceLabel(timelineSource(timelineEntry({ sourceSystem: 'fhc-his', eventType: 'NotificationUpdated', actorRole: 'system' }))),
    ).toBe('Text message or email')
    expect(sourceLabel(timelineSource(timelineEntry({ sourceSystem: 'his-simulator' })))).toBe('Hospital system')
  })

  it('shows what a nurse recorded after a call as recorded in CareLoop, not as a message from the patient', () => {
    const recorded = timelineEntry({
      sourceSystem: 'fhc-his',
      eventType: 'NotificationUpdated',
      actorReference: 'huda-saleh',
      actorRole: 'nurse-navigator',
    })
    expect(sourceLabel(timelineSource(recorded))).toBe('CareLoop')
  })

  it('names no patient app, portal or channel as a source', () => {
    for (const system of ['patient-channel', 'health-app', 'portal']) {
      expect(sourceLabel(timelineSource(timelineEntry({ sourceSystem: system, eventType: 'ClinicalReviewCompleted' })))).toBe(
        'Hospital system',
      )
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
    expect(timelineEntryTitle(refused)).toBe('Acknowledgement refused')
    expect(timelineEntryTitle({ ...refused, disposition: 'Applied', reason: 'EscalationAcknowledged applied' })).toBe(
      'Escalation acknowledged',
    )
    expect(timelineEntryTitle(timelineEntry({ eventType: 'ReferralUpdated', disposition: 'Rejected', reason: '' }))).toBe(
      'Referral update not accepted',
    )
  })

  it('uses the reason only when it says more than the kind of event, and invents nothing', () => {
    const bare = timelineEntry({ eventType: 'ReferralUpdated', reason: 'ReferralUpdated applied' })
    expect(hasOwnReason(bare)).toBe(false)
    expect(timelineEntryTitle(bare)).toBe('Referral updated')
    expect(
      timelineEntryTitle(timelineEntry({ reason: 'Overdue deadline checked; exception flags or escalation steps raised' })),
    ).toBe('Deadline reached')
    expect(timelineEntryTitle(timelineEntry({ reason: 'Deadline warning raised' }))).toBe('Deadline warning raised')
  })
})
