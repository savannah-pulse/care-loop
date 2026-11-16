import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { CaseTable } from './CaseTable'
import type { CaseRow } from './CaseTable'
import { sortCases } from './sortCases'

function row(caseId: string, exceptions: CaseRow['exceptions'], dueAt: string | null): CaseRow {
  return { caseId, exceptions, dueAt, patientReference: 'Patient/SYN-1', patientDisplayName: null,
    patientSecondaryIdentifier: null, pathwayId: 'breast-screening', displayLabel: 'PatientNotified',
    nextAction: 'review-result', urgency: 'source-reviewed', owner: {
      reference: 'SYN-STAFF-1', role: 'clinician', facilityId: 'SYN-FACILITY-1', isQueue: false,
    } }
}

describe('coordination worklist', () => {
  it('prioritizes overdue and flagged rows without mutating rows or reinterpreting urgency', () => {
    const rows = [row('on-time', [], '2026-01-01T00:00:00Z'), row('flagged', ['Unreachable'], null),
      row('overdue', ['Overdue'], '2026-01-02T00:00:00Z')]
    rows[0].urgency = 'urgent'
    expect(sortCases(rows).map((item) => item.caseId)).toEqual(['overdue', 'flagged', 'on-time'])
    expect(rows.map((item) => item.caseId)).toEqual(['on-time', 'flagged', 'overdue'])
    expect(rows[0].urgency).toBe('urgent')
  })
  it('renders the API display label and case ID when demographics are redacted', () => {
    const html = renderToStaticMarkup(<CaseTable rows={[row('case-1', ['Overdue'], null)]} onOpen={() => {}} />)
    expect(html).toContain('case-1')
    expect(html).toContain('Patient notified')
    expect(html).toContain('Overdue')
    expect(html).not.toContain('Patient/SYN-1')
  })
  it('orders rows without a deadline after dated rows, then by case id', () => {
    const rows = [row('b-none', ['Unassigned'], null), row('a-none', ['Unassigned'], null), row('dated', ['Unassigned'], '2026-01-05T00:00:00Z')]
    expect(sortCases(rows).map((item) => item.caseId)).toEqual(['dated', 'a-none', 'b-none'])
  })
  it('says so when a row has no deadline or next action', () => {
    const item = { ...row('case-1', ['Unassigned'], null), nextAction: null }
    const html = renderToStaticMarkup(<CaseTable rows={[item]} onOpen={() => {}} />)
    expect(html).toContain('No deadline set')
    expect(html).toContain('No next action')
  })
  it('renders staff demographics and an explicit empty state', () => {
    const item = { ...row('case-1', [], null), patientDisplayName: 'Synthetic Patient', patientSecondaryIdentifier: 'SYN-MRN-1' }
    const html = renderToStaticMarkup(<CaseTable rows={[item]} onOpen={() => {}} />)
    expect(html).toContain('Synthetic Patient')
    expect(html).toContain('SYN-MRN-1')
    expect(renderToStaticMarkup(<CaseTable rows={[]} onOpen={() => {}} />)).toContain('No matching cases')
  })
})
