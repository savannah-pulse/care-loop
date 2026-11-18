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
const MARIAM_BOWEL = caseSummary({
  ...MARIAM,
  caseId: '5e6f7a8b-4444-4444-8444-100245894005',
  pathwayId: 'bowel-screening',
})
const NAMESAKE = caseSummary({
  caseId: '6f7a8b9c-6666-4666-8666-100245896006',
  patientReference: 'p-100245896',
  patientDisplayName: 'Mariam Al Mansoori',
  patientSecondaryIdentifier: '100245896',
  exceptions: ['Overdue'],
})

/** The worklist's own case links, leaving out those on the "Start here today" card above it. */
function caseLinks(): HTMLElement[] {
  const startHere = screen.queryByRole('region', { name: 'Start here today' })
  return screen
    .getAllByRole('link')
    .filter((link) => link.getAttribute('href')?.startsWith('#/cases/') && !startHere?.contains(link))
}

/** Answers the role briefing the "Start here today" card asks for when it lists cases: AI is switched off. */
function briefingSwitchedOff(): void {
  answer('POST /ai/role-briefing', problem(501, 'RoleBriefing is switched off here.'))
}

function mrn(digits: string): string {
  return `MRN ${digits.slice(0, 3)}\u00a0${digits.slice(3, 6)}\u00a0${digits.slice(6)}`
}

describe('worklist', () => {
  it.each([
    ['in tables, on a wide screen', 1024],
    ['on cards, on a narrow screen', 640],
  ])('names every case by its patient and MRN %s, so no two share a name', async (_layout, width) => {
    answer('GET /cases', json([HESSA, NOURA, UNNAMED, MARIAM, MARIAM_BOWEL, NAMESAKE]))
    const { user } = renderApp({ at: '#/', as: 'huda-saleh', width })
    await user.click(await screen.findByRole('button', { name: /^Whole team/ }))
    await screen.findByRole('link', { name: `Hessa Al Dhaheri, ${mrn('100245891')}` })

    expect(screen.queryAllByRole('table').length > 0).toBe(width >= 900)
    const expected: ReadonlyArray<readonly [string, string]> = [
      [`Hessa Al Dhaheri, ${mrn('100245891')}`, HESSA.caseId],
      [`Noura Al Shamsi, ${mrn('100245892')}`, NOURA.caseId],
      ['Patient 100\u00a0245\u00a0893', UNNAMED.caseId],
      [`Mariam Al Mansoori, ${mrn('100245894')}, Breast screening`, MARIAM.caseId],
      [`Mariam Al Mansoori, ${mrn('100245894')}, Bowel screening`, MARIAM_BOWEL.caseId],
      [`Mariam Al Mansoori, ${mrn('100245896')}`, NAMESAKE.caseId],
    ]
    for (const [name, caseId] of expected) {
      const link = screen.getByRole('link', { name })
      expect(link).toHaveAttribute('href', `#/cases/${caseId}`)
      expect(name.startsWith(link.textContent ?? '')).toBe(true)
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
    await user.click(await screen.findByRole('button', { name: /^Whole team/ }))
    await screen.findByRole('link', { name: `Hessa Al Dhaheri, ${mrn('100245891')}` })

    await user.click(screen.getByRole('button', { name: "View another team's cases" }))
    const dialog = screen.getByRole('dialog', { name: "View another team's cases" })
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Team' }), 'regional-hospital')
    await user.click(within(dialog).getByRole('button', { name: 'Show their cases' }))
    expect(within(dialog).getByRole('alert')).toHaveTextContent('Choose a reason first.')
    expect(sentRequests('GET /cases').filter((request) => request.query.has('facilityId'))).toHaveLength(0)

    await user.click(within(dialog).getByRole('radio', { name: 'I am covering for a colleague there today' }))
    await user.click(within(dialog).getByRole('button', { name: 'Show their cases' }))

    expect(await screen.findByRole('link', { name: `Shamma Al Kaabi, ${mrn('100245895')}` })).toBeInTheDocument()
    const listRead = sentRequests('GET /cases').at(-1)
    expect(listRead?.query.get('facilityId')).toBe('regional-hospital')
    expect(listRead?.query.get('reason')).toBe('I am covering for a colleague there today')
    expect(screen.getByText('Showing cases at Regional Hospital')).toBeInTheDocument()
    expect(
      screen.getByText('Your reason is recorded with each case you open: I am covering for a colleague there today'),
    ).toBeInTheDocument()

    answer(`GET /cases/${theirs.caseId}`, json(caseDetail({ ...theirs })))
    answer(`GET /cases/${theirs.caseId}/timeline`, json([]))
    await user.click(screen.getByRole('link', { name: `Shamma Al Kaabi, ${mrn('100245895')}` }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Shamma Al Kaabi' })).toBeInTheDocument()
    for (const route of [`GET /cases/${theirs.caseId}`, `GET /cases/${theirs.caseId}/timeline`]) {
      expect(sentRequests(route).map((request) => request.query.get('reason'))).toEqual([
        'I am covering for a colleague there today',
      ])
    }
  })

  it('opens on the signed-in person’s own cases, with the whole team one press away', async () => {
    const rekhas = caseSummary({ ...HESSA, owner: owner({ reference: 'rekha-varghese' }) })
    const saras = caseSummary({ ...NOURA, owner: owner({ reference: 'sara-al-hammadi' }) })
    const callBack = caseSummary({ ...MARIAM, nextAction: 'call-patient', owner: owner({ reference: 'sara-al-hammadi' }) })
    answer('GET /cases', json([rekhas, saras, callBack]))
    briefingSwitchedOff()
    const { user } = renderApp({ at: '#/', as: 'rekha-varghese' })

    expect(await screen.findByRole('link', { name: `Hessa Al Dhaheri, ${mrn('100245891')}` })).toBeInTheDocument()
    expect(caseLinks()).toHaveLength(1)
    expect(screen.getByRole('button', { name: /^My cases\s*1$/ })).toHaveAttribute('aria-pressed', 'true')

    await user.click(screen.getByRole('button', { name: /^Whole team\s*3$/ }))
    expect(caseLinks()).toHaveLength(3)
    expect(sentRequests('GET /cases').every((request) => !request.query.has('ownerReference'))).toBe(true)
  })

  it('shows the nurse navigator the patients waiting for a call, and the facility lead what is escalated or unowned', async () => {
    const callBack = caseSummary({ ...MARIAM, nextAction: 'call-patient' })
    const escalated = caseSummary({ ...NOURA, isEscalated: true, exceptions: ['Overdue'] })
    const unowned = caseSummary({ ...NAMESAKE, exceptions: ['Unassigned'], owner: owner({ reference: 'facility-lead:family-health-centre', isQueue: true }) })
    answer('GET /cases', json([HESSA, callBack, escalated, unowned]))
    briefingSwitchedOff()

    const nurse = renderApp({ at: '#/', as: 'huda-saleh' })
    expect(await screen.findByRole('link', { name: `Mariam Al Mansoori, ${mrn('100245894')}` })).toBeInTheDocument()
    expect(caseLinks()).toHaveLength(1)
    nurse.unmount()

    renderApp({ at: '#/', as: 'joy-bautista' })
    expect(await screen.findByRole('link', { name: `Noura Al Shamsi, ${mrn('100245892')}` })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: `Mariam Al Mansoori, ${mrn('100245896')}` })).toBeInTheDocument()
    expect(caseLinks()).toHaveLength(2)
  })

  it('shows the lab the cases whose follow-up is under way with no outcome recorded yet', async () => {
    const awaited = caseSummary({ ...NOURA, exceptions: [], nextAction: 'arrange-follow-up' })
    const referred = caseSummary({ ...MARIAM, nextAction: 'respond-to-referral' })
    const decided = caseSummary({ ...NAMESAKE, exceptions: [], stage: 'ClosureReview', displayLabel: 'ClosureReview', nextAction: 'decide-closure' })
    answer('GET /cases', json([awaited, referred, decided]))
    briefingSwitchedOff()
    renderApp({ at: '#/', as: 'khalid-noor' })

    expect(await screen.findByRole('link', { name: `Noura Al Shamsi, ${mrn('100245892')}` })).toBeInTheDocument()
    expect(caseLinks()).toHaveLength(1)
    expect(screen.getByRole('button', { name: /^Whole team\s*3$/ })).toBeInTheDocument()
  })

  it('says so when nothing waits for the person, and offers the team’s list', async () => {
    answer('GET /cases', json([HESSA]))
    const { user } = renderApp({ at: '#/', as: 'huda-saleh' })

    expect(await screen.findByText('No open case is waiting for you. Your team has 1 open case.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Show the whole team' }))
    expect(await screen.findByRole('link', { name: `Hessa Al Dhaheri, ${mrn('100245891')}` })).toBeInTheDocument()
  })

  it('lets the programme coordinator search by case number, and never shows or searches names', async () => {
    const first = caseSummary({ ...HESSA, isEscalated: true })
    const second = caseSummary({ ...NOURA, isEscalated: true, exceptions: ['Overdue'] })
    answer('GET /cases', json([first, second]))
    briefingSwitchedOff()
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

    for (const identifier of ['100245892', 'p-100245892']) {
      await user.clear(search)
      await user.type(search, identifier)
      expect(screen.getByRole('heading', { name: 'No cases match these filters' })).toBeInTheDocument()
    }
    expect(document.body.textContent).not.toMatch(/100245891|100245892|MRN\s\d/)
    expect(document.querySelector('[title*="p-1002458"]')).toBeNull()
  })
})
