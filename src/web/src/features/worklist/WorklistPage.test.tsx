import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { answer, json, problem, sentRequests } from '../../testing/api'
import { caseDetail, caseSummary, owner } from '../../testing/builders'
import { renderApp } from '../../testing/screens'

const HESSA = caseSummary({ caseId: '1a2b3c4d-1111-4111-8111-100245891001', exceptions: ['Overdue'] })
const NOURA = caseSummary({
  caseId: '2b3c4d5e-2222-4222-8222-100245892002',
  patientReference: 'p-100245892',
  patientDisplayName: 'Noura Al Shamsi',
  patientSecondaryIdentifier: '100245892',
  exceptions: ['Unreachable'],
})
const UNNAMED = caseSummary({
  caseId: '3c4d5e6f-3333-4333-8333-100245893003',
  patientReference: 'p-100245893',
  patientDisplayName: null,
  patientSecondaryIdentifier: null,
  dueAt: '2026-11-13T05:00:00Z',
})
const MARIAM = caseSummary({
  caseId: '4d5e6f7a-4444-4444-8444-100245894004',
  patientReference: 'p-100245894',
  patientDisplayName: 'Mariam Al Mansoori',
  patientSecondaryIdentifier: '100245894',
  urgency: 'PLACEHOLDER-URGENCY-B',
})

function caseLinks(): HTMLElement[] {
  return screen.getAllByRole('link').filter((link) => link.getAttribute('href')?.startsWith('#/cases/'))
}

describe('worklist', () => {
  it.each([
    ['in tables, on a wide screen', 1024],
    ['on cards, on a narrow screen', 640],
  ])('names every case by its patient %s, so no two share a name', async (_layout, width) => {
    answer('GET /cases', json([HESSA, NOURA, UNNAMED, MARIAM]))
    renderApp({ at: '#/', as: 'huda-saleh', width })
    await screen.findByRole('link', { name: 'Hessa Al Dhaheri' })

    expect(screen.queryAllByRole('table').length > 0).toBe(width >= 900)
    const expected: ReadonlyArray<readonly [string | RegExp, string]> = [
      ['Hessa Al Dhaheri', HESSA.caseId],
      ['Noura Al Shamsi', NOURA.caseId],
      [/^Patient 100.245.893$/, UNNAMED.caseId],
      ['Mariam Al Mansoori', MARIAM.caseId],
    ]
    for (const [name, caseId] of expected) {
      expect(screen.getByRole('link', { name })).toHaveAttribute('href', `#/cases/${caseId}`)
    }
    expect(caseLinks()).toHaveLength(expected.length)
  })

  it('asks why before showing another team’s cases, then sends the reason and the team with every read', async () => {
    const theirs = caseSummary({
      caseId: '5e6f7a8b-5555-4555-8555-100245895005',
      patientReference: 'p-100245895',
      patientDisplayName: 'Shamma Al Kaabi',
      patientSecondaryIdentifier: '100245895',
      facilityId: 'regional-hospital',
      exceptions: ['Overdue'],
      owner: owner({ reference: 'regional-clinician', facilityId: 'regional-hospital' }),
    })
    answer('GET /cases', (request) => {
      const facilityId = request.query.get('facilityId')
      if (facilityId && !request.query.get('reason')) {
        return problem(403, 'Give a reason to read another facility’s cases.')
      }
      return json(facilityId === 'regional-hospital' ? [theirs] : [HESSA])
    })
    const { user } = renderApp({ at: '#/', as: 'huda-saleh' })
    await screen.findByRole('link', { name: 'Hessa Al Dhaheri' })

    await user.click(screen.getByRole('button', { name: "View another team's cases" }))
    const dialog = screen.getByRole('dialog', { name: "View another team's cases" })
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Team' }), 'regional-hospital')
    await user.click(within(dialog).getByRole('button', { name: 'Show their cases' }))
    expect(within(dialog).getByRole('alert')).toHaveTextContent('Choose a reason first.')
    expect(sentRequests('GET /cases').filter((request) => request.query.has('facilityId'))).toHaveLength(0)

    await user.click(within(dialog).getByRole('radio', { name: 'I am covering for a colleague there today' }))
    await user.click(within(dialog).getByRole('button', { name: 'Show their cases' }))

    expect(await screen.findByRole('link', { name: 'Shamma Al Kaabi' })).toBeInTheDocument()
    const listRead = sentRequests('GET /cases').at(-1)
    expect(listRead?.query.get('facilityId')).toBe('regional-hospital')
    expect(listRead?.query.get('reason')).toBe('I am covering for a colleague there today')
    expect(screen.getByText('Showing cases at Regional Hospital')).toBeInTheDocument()
    expect(
      screen.getByText('Your reason is recorded with each case you open: I am covering for a colleague there today'),
    ).toBeInTheDocument()

    answer(`GET /cases/${theirs.caseId}`, json(caseDetail({ ...theirs })))
    answer(`GET /cases/${theirs.caseId}/timeline`, json([]))
    await user.click(screen.getByRole('link', { name: 'Shamma Al Kaabi' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Shamma Al Kaabi' })).toBeInTheDocument()
    for (const route of [`GET /cases/${theirs.caseId}`, `GET /cases/${theirs.caseId}/timeline`]) {
      expect(sentRequests(route).map((request) => request.query.get('reason'))).toEqual([
        'I am covering for a colleague there today',
      ])
    }
  })

  it('lets the programme coordinator search by case number, and never shows or searches names', async () => {
    const first = caseSummary({ ...HESSA, isEscalated: true })
    const second = caseSummary({ ...NOURA, isEscalated: true, exceptions: ['Overdue'] })
    answer('GET /cases', json([first, second]))
    const { user } = renderApp({ at: '#/', as: 'obaid-al-suwaidi' })

    expect(await screen.findByRole('link', { name: 'Case 1A2B3C4D' })).toBeInTheDocument()
    expect(screen.getAllByText('Name hidden for your role')).toHaveLength(2)
    const search = screen.getByRole('searchbox', { name: 'Search by case number' })
    expect(search).toHaveAttribute('placeholder', 'Search by case number')

    await user.type(search, '2b3c 4d5e')
    expect(screen.getByRole('link', { name: 'Case 2B3C4D5E' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Case 1A2B3C4D' })).not.toBeInTheDocument()
    expect(screen.getByText('Showing 1 of 2 open cases.')).toBeInTheDocument()

    await user.clear(search)
    await user.type(search, 'Noura')
    expect(screen.getByRole('heading', { name: 'No cases match these filters' })).toBeInTheDocument()
    expect(screen.queryByText(/Noura|Hessa/)).not.toBeInTheDocument()
  })
})
