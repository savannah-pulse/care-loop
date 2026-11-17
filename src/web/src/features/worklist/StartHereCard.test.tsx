import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { STAFF, type StaffMember } from '../../identity'
import { answer, json, problem, sentRequests } from '../../testing/api'
import { TEST_NOW, aiText, caseSummary, leadQueue, owner } from '../../testing/builders'
import { renderApp } from '../../testing/screens'
import { startHere } from './startHere'
import { toWorklistRows } from './worklist'

const BRIEFING = 'POST /ai/role-briefing'
const CAPTION = 'Written by AI from your worklist.'

/** Dr. Rekha's cases at 09:00 in Abu Dhabi: one late and escalated, one urgent, two due today, one later. */
const LATE = caseSummary({
  caseId: '1a2b3c4d-1111-4111-8111-100245891001',
  nextAction: 'arrange-alternative-referral',
  dueAt: '2026-11-11T11:00:00Z',
  exceptions: ['Overdue', 'Rejected'],
  isEscalated: true,
})
const URGENT = caseSummary({
  caseId: '2b3c4d5e-2222-4222-8222-100318402002',
  patientReference: 'p-100318402',
  patientDisplayName: 'Noura Al Shamsi',
  patientSecondaryIdentifier: '100318402',
  urgency: 'PLACEHOLDER-URGENCY-B',
  dueAt: '2026-11-13T05:00:00Z',
})
const TODAY = caseSummary({
  caseId: '3c4d5e6f-3333-4333-8333-100377615003',
  patientReference: 'p-100377615',
  patientDisplayName: 'Shamma Al Mheiri',
  patientSecondaryIdentifier: '100377615',
  nextAction: 'review-result',
  dueAt: '2026-11-12T13:00:00Z',
})
const LATER_TODAY = caseSummary({
  caseId: '4d5e6f7a-4444-4444-8444-100402233004',
  patientReference: 'p-100402233',
  patientDisplayName: 'Fatima Al Zaabi',
  patientSecondaryIdentifier: '100402233',
  dueAt: '2026-11-12T15:00:00Z',
})
const NEXT_WEEK = caseSummary({
  caseId: '5e6f7a8b-5555-4555-8555-100455790005',
  patientReference: 'p-100455790',
  patientDisplayName: 'Maricel Reyes',
  patientSecondaryIdentifier: '100455790',
  dueAt: '2026-11-16T05:00:00Z',
})
const SARAS = caseSummary({
  caseId: '6f7a8b9c-6666-4666-8666-100468104006',
  patientReference: 'p-100468104',
  patientDisplayName: 'Aisha Al Nuaimi',
  patientSecondaryIdentifier: '100468104',
  owner: owner({ reference: 'sara-al-hammadi' }),
  exceptions: ['Overdue'],
  dueAt: '2026-11-11T05:00:00Z',
})

function person(id: string): StaffMember {
  const found = STAFF.find((member) => member.id === id)
  if (!found) {
    throw new Error(`No such person: ${id}`)
  }
  return found
}

function card(): HTMLElement {
  return screen.getByRole('region', { name: 'Start here today' })
}

async function openAsRekha(): Promise<HTMLElement> {
  answer('GET /cases', json([NEXT_WEEK, SARAS, TODAY, LATER_TODAY, URGENT, LATE]))
  renderApp({ at: '#/', as: 'rekha-varghese' })
  return screen.findByRole('region', { name: 'Start here today' })
}

function linkNames(region: HTMLElement): string[] {
  return within(region)
    .getAllByRole('link')
    .map((link) => link.getAttribute('aria-label') ?? link.textContent ?? '')
}

describe('start here today', () => {
  it('lists the signed-in person’s cases overdue or escalated first, then urgent, then due today, three at most', () => {
    const rows = toWorklistRows([NEXT_WEEK, SARAS, TODAY, LATER_TODAY, URGENT, LATE], 'clinician')
    expect(startHere(rows, person('rekha-varghese'), TEST_NOW).map((item) => [item.row.summary.caseId, item.reason])).toEqual([
      [LATE.caseId, 'overdue'],
      [URGENT.caseId, 'urgent'],
      [TODAY.caseId, 'dueToday'],
    ])
    const queued = toWorklistRows([caseSummary({ owner: leadQueue(), exceptions: ['Unassigned'], dueAt: '2026-11-12T07:00:00Z' })], 'facility-lead')
    expect(startHere(queued, person('joy-bautista'), TEST_NOW).map((item) => item.reason)).toEqual(['dueToday'])
    expect(startHere(queued, person('rekha-varghese'), TEST_NOW)).toEqual([])
  })

  it('links each case and adds one sentence written by AI, asked for with the person’s identity', async () => {
    answer(BRIEFING, json(aiText({ text: 'One case is late and already escalated, so start there.' })))
    const region = await openAsRekha()

    expect(within(region).getByRole('heading', { level: 2, name: 'Start here today' })).toBeInTheDocument()
    expect(within(region).getByText('Worked out by CareLoop from your deadlines, not by AI')).toBeInTheDocument()
    expect(linkNames(region)).toEqual([
      'Hessa Al Dhaheri, MRN 100\u00a0245\u00a0891, Overdue',
      'Noura Al Shamsi, MRN 100\u00a0318\u00a0402, Urgent',
      'Shamma Al Mheiri, MRN 100\u00a0377\u00a0615, Due today',
    ])
    expect(within(region).getByRole('link', { name: /^Hessa Al Dhaheri.*Overdue$/ })).toHaveAttribute('href', `#/cases/${LATE.caseId}`)
    expect(within(region).getByText('Case 1A2B3C4D')).toBeInTheDocument()
    expect(within(region).getByText('Send a new referral')).toBeInTheDocument()

    const words = await within(region).findByRole('figure', { name: CAPTION })
    expect(words).toHaveTextContent('One case is late and already escalated, so start there.')
    const [request, ...more] = sentRequests(BRIEFING)
    expect(more).toHaveLength(0)
    expect(request.headers.get('X-Demo-User')).toBe('rekha-varghese')
    expect(request.headers.get('X-Demo-Role')).toBe('clinician')
    expect(request.headers.get('X-Demo-Facility')).toBe('family-health-centre')
    expect(request.query.toString()).toBe('')
  })

  it('shows a skeleton line while AI writes, and the cases straight away', async () => {
    let release: (response: Response) => void = () => undefined
    answer(BRIEFING, () => new Promise<Response>((resolve) => (release = resolve)))
    const region = await openAsRekha()

    expect(within(region).getAllByRole('listitem')).toHaveLength(3)
    expect(within(region).getByRole('status')).toHaveTextContent('Loading a sentence written by AI')
    release(json(aiText()))
    expect(await within(region).findByRole('figure', { name: CAPTION })).toBeInTheDocument()
  })

  it('leaves the AI line out while AI is switched off (501)', async () => {
    answer(BRIEFING, problem(501, 'RoleBriefing is switched off here.'))
    const region = await openAsRekha()

    await screen.findByText('Case 1A2B3C4D')
    await expect.poll(() => sentRequests(BRIEFING).length).toBe(1)
    expect(within(region).getAllByRole('listitem')).toHaveLength(3)
    expect(within(region).queryByRole('figure')).not.toBeInTheDocument()
    expect(within(region).queryByText(/AI could not|Written by AI/)).not.toBeInTheDocument()
  })

  it('says calmly that AI could not write it (503), and still lists every case', async () => {
    answer(BRIEFING, problem(503, 'AI could not write this'))
    const region = await openAsRekha()

    expect(
      await within(region).findByText('AI could not sum this up this time. The cases above come from CareLoop and are up to date.'),
    ).toBeInTheDocument()
    expect(within(region).getAllByRole('listitem')).toHaveLength(3)
    expect(linkNames(region)).toHaveLength(3)
    expect(within(region).queryByRole('figure')).not.toBeInTheDocument()
  })

  it('says so in one calm line when nothing of the person’s is due, and asks no AI', async () => {
    answer('GET /cases', json([NEXT_WEEK, SARAS]))
    renderApp({ at: '#/', as: 'rekha-varghese' })

    const region = await screen.findByRole('region', { name: 'Start here today' })
    expect(region).toHaveTextContent('Nothing of yours is overdue, urgent or due today. Your next deadline is Mon 16 Nov, 09:00.')
    expect(within(region).queryByRole('link')).not.toBeInTheDocument()
    expect(sentRequests(BRIEFING)).toHaveLength(0)
  })

  it('shows the programme coordinator case numbers only, never a name', async () => {
    answer('GET /cases', json([caseSummary({ ...LATE, patientDisplayName: null, patientSecondaryIdentifier: null, patientReference: null })]))
    answer(BRIEFING, json(aiText({ text: 'One escalated case is waiting for you.' })))
    renderApp({ at: '#/', as: 'obaid-al-suwaidi' })

    const region = await screen.findByRole('region', { name: 'Start here today' })
    expect(linkNames(region)).toEqual(['Case 1A2B3C4D, Overdue'])
    expect(region.textContent).not.toMatch(/Hessa|MRN/)
    expect(await within(region).findByRole('figure', { name: CAPTION })).toHaveTextContent('One escalated case is waiting for you.')
    expect(sentRequests(BRIEFING)[0].headers.has('X-Demo-Facility')).toBe(false)
    expect(card()).toBe(region)
  })
})
