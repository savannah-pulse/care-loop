import { describe, expect, it } from 'vitest'
import { programme } from '../../testing/builders'
import {
  distinctCitations,
  evidenceKindName,
  isZeroDuration,
  ladderInOrder,
  programmeApproval,
  settingDuration,
  settingText,
  sharedCitation,
} from './programmes'

describe('programme settings (C1)', () => {
  it('says a programme is waiting for approval while anything in it is a placeholder', () => {
    const deadlines = programme().deadlines.map((deadline) => ({ ...deadline, clinicallyApproved: true }))
    const escalationLadder = programme().escalationLadder.map((step) => ({ ...step, clinicallyApproved: true }))
    expect(programmeApproval(programme())).toBe('waiting')
    expect(programmeApproval(programme({ isPlaceholder: false, deadlines, escalationLadder }))).toBe('approved')
    expect(programmeApproval(programme({ isPlaceholder: true, deadlines, escalationLadder }))).toBe('waiting')
    expect(programmeApproval(programme({ isPlaceholder: false, deadlines }))).toBe('waiting')
  })

  it('words deadlines and reminder times exactly as configured, and never makes one up', () => {
    expect(settingDuration('1.00:00:00')).toBe('1 day')
    expect(settingDuration('04:00:00')).toBe('4 h')
    expect(settingDuration('00:30:00')).toBe('30 min')
    expect(settingDuration('00:00:48')).toBe('48 s')
    expect(settingDuration('PT2H30M')).toBe('2 h 30 min')
    expect(settingDuration('next week')).toBe('next week')
    expect(isZeroDuration('00:00:00')).toBe(true)
    expect(isZeroDuration('1.00:00:00')).toBe(false)
  })

  it('orders the ladder by level, even when a level arrives as text', () => {
    const [first, second] = programme().escalationLadder
    expect(ladderInOrder([{ ...second, level: '2' }, first]).map((step) => step.escalateToRole)).toEqual([
      'facility-lead',
      'coordinator',
    ])
  })

  it('words a kind of evidence', () => {
    expect(evidenceKindName('treatment-plan')).toBe('Treatment plan')
  })

  it('says "placeholder" in words instead of repeating the capitalised marker', () => {
    expect(settingText('PLACEHOLDER - no clinical source yet; to be cited by the clinical adviser')).toEqual({
      text: 'No clinical source yet; to be cited by the clinical adviser',
      placeholder: true,
    })
    expect(settingText('PLACEHOLDER - Breast screening follow-up (Abu Dhabi prototype)').text).toBe(
      'Breast screening follow-up (Abu Dhabi prototype)',
    )
    expect(settingText('Screening guideline 2026, section 4')).toEqual({
      text: 'Screening guideline 2026, section 4',
      placeholder: false,
    })
  })

  it('shows a citation once when every item shares it', () => {
    expect(sharedCitation(['Guideline A', 'Guideline A'])).toBe('Guideline A')
    expect(sharedCitation(['Guideline A', 'Guideline B'])).toBeUndefined()
    expect(sharedCitation([null, null])).toBeUndefined()
    expect(sharedCitation([])).toBeUndefined()
  })

  it('lists different citations once each, in order, to number them', () => {
    expect(distinctCitations(['Guideline A', null, 'Guideline B', 'Guideline A', ' '])).toEqual(['Guideline A', 'Guideline B'])
  })
})
