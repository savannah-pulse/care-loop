import { describe, expect, it } from 'vitest'
import { caseDetail, leadQueue, owner } from '../../testing/builders'
import { whoActs } from './whoActs'

const HUDA = { reference: 'huda-saleh', role: 'nurse-navigator' }
const REKHA = { reference: 'rekha-varghese', role: 'clinician' }
const LAYLA = { reference: 'layla-haddad', role: 'receiving-clinician' }
const JOY = { reference: 'joy-bautista', role: 'facility-lead' }
const IN_HOSPITAL = 'CareLoop moves on once it is recorded there.'

describe('whoActs', () => {
  const callBack = caseDetail({ nextAction: 'call-patient' })

  it('tells a nurse navigator to make the call and record it, while the doctor stays responsible', () => {
    expect(whoActs(callBack, HUDA)).toBe(
      'Your step: call Hessa Al Dhaheri, then record it in "After a call with the patient". Dr. Rekha Varghese stays responsible for the case.',
    )
  })

  it('does not repeat who is responsible to a nurse navigator who is', () => {
    const own = caseDetail({ nextAction: 'call-patient', owner: owner({ reference: 'huda-saleh', role: 'nurse-navigator' }) })
    expect(whoActs(own, HUDA)).toBe('Your step: call Hessa Al Dhaheri, then record it in "After a call with the patient".')
  })

  it('tells the doctor responsible that a nurse navigator makes the call', () => {
    expect(whoActs(callBack, REKHA)).toBe(
      'A nurse navigator calls the patient and records the call here. You stay responsible for the case.',
    )
  })

  it('says a step of the person responsible is done in the hospital system', () => {
    const review = caseDetail({ nextAction: 'review-result' })
    expect(whoActs(review, REKHA)).toBe(`You do this in the hospital system. ${IN_HOSPITAL}`)
    expect(whoActs(review, HUDA)).toBe(`Dr. Rekha Varghese does this in the hospital system. ${IN_HOSPITAL}`)
  })

  it('names the receiving clinician a referral waits for, not the sender who stays responsible', () => {
    const referral = caseDetail({
      nextAction: 'respond-to-referral',
      proposedOwner: owner({ reference: 'layla-haddad', role: 'receiving-clinician', facilityId: 'general-hospital' }),
    })
    expect(whoActs(referral, LAYLA)).toBe(`You do this in the hospital system. ${IN_HOSPITAL}`)
    expect(whoActs(referral, REKHA)).toBe(`Dr. Layla Haddad does this in the hospital system. ${IN_HOSPITAL}`)
  })

  it('names the role when nobody on the case holds it, and who stays responsible', () => {
    const consultation = caseDetail({ nextAction: 'answer-consultation' })
    expect(whoActs(consultation, HUDA)).toBe(
      `The receiving clinician does this in the hospital system. ${IN_HOSPITAL} Dr. Rekha Varghese stays responsible for the case.`,
    )
    const unowned = caseDetail({ nextAction: 'assign-owner', owner: leadQueue() })
    expect(whoActs(unowned, JOY)).toBe(`You do this in the hospital system. ${IN_HOSPITAL}`)
    expect(whoActs(unowned, HUDA)).toBe(`The facility lead does this in the hospital system. ${IN_HOSPITAL}`)
  })

  it('keeps the plain wording for a step it has no words for', () => {
    expect(whoActs(caseDetail({ nextAction: 'something-new' }), REKHA)).toBe('You need to do this.')
  })
})
