import { describe, expect, it } from 'vitest'
import { caseSummary, timelineEntry } from '../testing/builders'
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
  it('names where each entry came from in plain words', () => {
    expect(sourceLabel(timelineSource(timelineEntry({ sourceSystem: 'careloop' })))).toBe('CareLoop')
    expect(sourceLabel(timelineSource(timelineEntry({ sourceSystem: 'patient-channel' })))).toBe('Patient channel')
    expect(sourceLabel(timelineSource(timelineEntry({ sourceSystem: 'his-simulator' })))).toBe('Hospital system')
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
