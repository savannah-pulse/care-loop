import { describe, expect, it } from 'vitest'
import { ROLE_KEYS, isCallOutcomeRole } from '../../identity'
import { caseDetail } from '../../testing/builders'
import { EMPTY_CALL_OUTCOME, callOutcomeOffer, canRecord, recordedConfidence, toCallOutcome } from './callOutcome'

const notified = caseDetail({
  stage: 'FollowUpActive',
  tracks: [{ kind: 'Notification', status: 'Delivered', isComplete: false, updatedAt: '2026-11-11T05:00:00Z' }],
})

describe('who records a call outcome', () => {
  it('is the nurse navigator and nobody else', () => {
    expect(ROLE_KEYS.filter(isCallOutcomeRole)).toEqual(['nurse-navigator'])
    expect(isCallOutcomeRole('patient')).toBe(false)
  })

  it('offers the card to a nurse navigator once a message has been sent to the patient', () => {
    expect(callOutcomeOffer(notified, 'nurse-navigator')).toBe('open')
  })

  it('says not yet when no message has been sent', () => {
    expect(callOutcomeOffer(caseDetail({ tracks: [] }), 'nurse-navigator')).toBe('notYet')
    const notStarted = caseDetail({ tracks: [{ kind: 'Notification', status: null, isComplete: false, updatedAt: null }] })
    expect(callOutcomeOffer(notStarted, 'nurse-navigator')).toBe('notYet')
  })

  it.each(['clinician', 'receiving-clinician', 'facility-lead', 'coordinator', 'lab'])('hides the card from a %s', (role) => {
    expect(callOutcomeOffer(notified, role)).toBe('hidden')
  })

  it('hides the card on a closed case', () => {
    expect(callOutcomeOffer({ ...notified, stage: 'Closed' }, 'nurse-navigator')).toBe('hidden')
  })
})

describe('what can be recorded', () => {
  it('needs at least one thing the patient said', () => {
    expect(canRecord(EMPTY_CALL_OUTCOME)).toBe(false)
    expect(canRecord({ willCome: true, saidBack: false })).toBe(true)
    expect(canRecord({ willCome: false, saidBack: true })).toBe(true)
    expect(canRecord({ willCome: false, saidBack: false, confidence: 3 })).toBe(true)
  })

  it.each([0, 6, 2.5, Number.NaN])('refuses a rating of %s', (confidence) => {
    expect(canRecord({ willCome: true, saidBack: true, confidence })).toBe(false)
  })

  it('keeps the three outcomes apart and never invents a rating', () => {
    expect(toCallOutcome({ willCome: false, saidBack: true })).toEqual({
      acknowledged: false,
      understood: true,
      confidence: null,
    })
    expect(toCallOutcome({ willCome: true, saidBack: false, confidence: 5 })).toEqual({
      acknowledged: true,
      understood: false,
      confidence: 5,
    })
  })

  it('reads the rating on record as a number from 1 to 5, or none', () => {
    expect(recordedConfidence(caseDetail({ confidence: 4 }))).toBe(4)
    expect(recordedConfidence(caseDetail({ confidence: '2' }))).toBe(2)
    expect(recordedConfidence(caseDetail({ confidence: null }))).toBeUndefined()
    expect(recordedConfidence(caseDetail({ confidence: 9 }))).toBeUndefined()
  })
})
