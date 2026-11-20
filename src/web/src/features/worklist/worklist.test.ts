import { describe, expect, it } from 'vitest'
import { urgencyText } from '../../domain'
import { TEST_NOW, caseSummary, leadQueue } from '../../testing/builders'
import { countOpen, groupRows, isMine, matchesFilter, matchesSearch, toWorklistRows } from './worklist'

const cases = [
  caseSummary({ caseId: 'later', dueAt: '2026-11-13T05:00:00Z' }),
  caseSummary({ caseId: 'flagged', exceptions: ['Unreachable'], dueAt: null }),
  caseSummary({ caseId: 'today', dueAt: '2026-11-12T10:00:00Z' }),
  caseSummary({ caseId: 'overdue', exceptions: ['Overdue'], dueAt: '2026-11-11T11:00:00Z' }),
  caseSummary({ caseId: 'urgent', urgency: 'PLACEHOLDER-URGENCY-B', dueAt: '2026-11-12T12:00:00Z' }),
  caseSummary({ caseId: 'escalated', isEscalated: true, dueAt: '2026-11-12T09:00:00Z' }),
  caseSummary({ caseId: 'closed', stage: 'Closed', displayLabel: 'Closed', closureReason: 'Treated', dueAt: null }),
]

describe('worklist', () => {
  it('groups open cases by what needs action now, puts overdue and urgent cases first and leaves its input alone', () => {
    const rows = toWorklistRows(cases, 'nurse-navigator')
    const order = rows.map((row) => row.summary.caseId)
    const sections = groupRows(rows, TEST_NOW)
    expect(sections.map((section) => [section.group, section.rows.map((row) => row.summary.caseId)])).toEqual([
      ['now', ['overdue', 'urgent', 'escalated']],
      ['today', ['today']],
      ['later', ['later', 'flagged']],
    ])
    expect(rows.map((row) => row.summary.caseId)).toEqual(order)
    expect(countOpen(rows, TEST_NOW)).toEqual({ open: 6, overdue: 1, dueToday: 3 })
  })

  it('never reinterprets an urgency code it does not know', () => {
    const [row] = toWorklistRows([caseSummary({ urgency: 'SITE-PRIORITY-1' })], 'clinician')
    expect(row.urgent).toBe(false)
    expect(row.summary.urgency).toBe('SITE-PRIORITY-1')
    expect(urgencyText(row.summary.urgency)).toBe('SITE-PRIORITY-1')
  })

  it('shows the display stage the API sends, while the exception decides the queue', () => {
    const [row] = toWorklistRows(
      [caseSummary({ owner: leadQueue(), displayLabel: 'OwnerAssigned', exceptions: ['Unassigned'], nextAction: 'assign-owner' })],
      'facility-lead',
    )
    expect(row.stage).toBe('Owner assigned')
    expect(row.status.label).toBe('Needs a new owner')
    expect(matchesFilter(row, 'needsOwner')).toBe(true)
  })

  it('finds a patient by name or MRN, with or without spaces', () => {
    const [row] = toWorklistRows([caseSummary()], 'nurse-navigator')
    expect(matchesSearch(row, 'hessa')).toBe(true)
    expect(matchesSearch(row, '100 245')).toBe(true)
    expect(matchesSearch(row, '100245891')).toBe(true)
    expect(matchesSearch(row, 'noura')).toBe(false)
  })

  it('finds a redacted case by its case number only', () => {
    const [row] = toWorklistRows([caseSummary({ patientDisplayName: null, patientSecondaryIdentifier: null })], 'coordinator')
    expect(row.patient.hidden).toBe(true)
    expect(matchesSearch(row, '3f6b')).toBe(true)
    expect(matchesSearch(row, 'p-100245891')).toBe(false)
  })

  it('has nothing to group when there are no open cases', () => {
    const rows = toWorklistRows([cases[6]], 'nurse-navigator')
    expect(groupRows(rows, TEST_NOW)).toEqual([])
    expect(countOpen(rows, TEST_NOW)).toEqual({ open: 0, overdue: 0, dueToday: 0 })
  })
})

describe('isMine', () => {
  it("puts each of the nurse navigator's steps on the nurse's own list, whoever is responsible", () => {
    const steps = ['call-patient', 'acknowledge-notification', 'contact-patient-another-way', 'arrange-follow-up']
    const cases = steps.map((nextAction, i) => caseSummary({ caseId: `3f6b1c2a-8d4e-4f5a-9b7c-10024589100${i}`, nextAction }))
    const nurse = { reference: 'huda-saleh', role: 'nurse-navigator' }
    const mine = toWorklistRows(cases, nurse.role).filter((row) => isMine(row, nurse))
    expect(mine.map((row) => row.summary.nextAction).sort()).toEqual(['acknowledge-notification', 'call-patient', 'contact-patient-another-way'])
  })
})
