import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { CaseDetail } from '../../data'
import { answer, json, problem, sentRequests } from '../../testing/api'
import { TEST_NOW, caseDetail, caseSummary, escalation, owner, programme, timelineEntry } from '../../testing/builders'
import { renderApp } from '../../testing/screens'

const TO_LEAD = escalation()
const TO_COORDINATOR = escalation({
  escalationId: '5d1e2f3a-4b5c-4d6e-8f70-100245891e02',
  level: 2,
  escalatedTo: { reference: 'coordinator:family-health-centre', role: 'coordinator', facilityId: 'family-health-centre', isQueue: true },
  raisedAt: '2026-11-12T04:00:00Z',
})
const ESCALATED = caseDetail({ isEscalated: true, exceptions: ['Overdue'], escalations: [TO_LEAD] })
const WITH_COORDINATOR = caseDetail({ isEscalated: true, exceptions: ['Overdue'], escalations: [TO_LEAD, TO_COORDINATOR] })
const CASE_ID = ESCALATED.caseId
const ESCALATION_PAGE = `#/cases/${CASE_ID}/escalation`
const CASE_ROUTE = `GET /cases/${CASE_ID}`
const ACK_ROUTE = `POST /cases/${CASE_ID}/escalations/${TO_LEAD.escalationId}/acknowledgement`

function answerReads(detail: CaseDetail): void {
  answer(`GET /cases/${detail.caseId}`, json(detail))
  answer(`GET /cases/${detail.caseId}/timeline`, json([]))
  answer('GET /programmes/breast-screening', json(programme()))
  answer('GET /cases', json([caseSummary({ ...detail })]))
}

function acknowledgementCard(): HTMLElement {
  return screen.getByRole('region', { name: 'What acknowledging means' })
}

async function openEscalation(detail: CaseDetail, as: string) {
  answerReads(detail)
  const app = renderApp({ at: `#/cases/${detail.caseId}/escalation`, as })
  await screen.findByRole('region', { name: 'What acknowledging means' })
  return app
}

describe('escalation', () => {
  it.each([
    ['shows Acknowledge to the facility lead of the case’s own facility', 'joy-bautista', ESCALATED, true],
    ['hides Acknowledge from the nurse navigator', 'huda-saleh', ESCALATED, false],
    ['hides Acknowledge from a clinician', 'rekha-varghese', ESCALATED, false],
    ['shows Acknowledge to the programme coordinator once the latest escalation reaches them', 'obaid-al-suwaidi', WITH_COORDINATOR, true],
    ['hides Acknowledge from the facility lead once the latest escalation has gone to the coordinator', 'joy-bautista', WITH_COORDINATOR, false],
  ])('%s', async (_title, as, detail, offered) => {
    await openEscalation(detail, as)
    const button = within(acknowledgementCard()).queryByRole('button', { name: 'Acknowledge' })
    if (offered) {
      expect(button).toBeInTheDocument()
    } else {
      expect(button).not.toBeInTheDocument()
      expect(acknowledgementCard()).toHaveTextContent(
        /Only the (facility lead at Family Health Centre|programme coordinator) can acknowledge this escalation\./,
      )
    }
  })

  it('never offers Acknowledge to the lead of another facility, even after they give a reason', async () => {
    const elsewhere = caseDetail({
      facilityId: 'general-hospital',
      owner: owner({ reference: 'layla-haddad', role: 'receiving-clinician', facilityId: 'general-hospital' }),
      isEscalated: true,
      exceptions: ['Overdue'],
      escalations: [escalation({ escalatedTo: { reference: 'facility-lead:general-hospital', role: 'facility-lead', facilityId: 'general-hospital', isQueue: true } })],
    })
    const withReason = (body: CaseDetail | []) => (request: { query: URLSearchParams }) =>
      request.query.get('reason') ? json(body) : problem(403, 'Give a reason to read a case outside your facility.')
    answer(CASE_ROUTE, withReason(elsewhere))
    answer(`GET /cases/${CASE_ID}/timeline`, withReason([]))
    answer('GET /programmes/breast-screening', json(programme()))
    answer('GET /cases', json([]))
    const { user } = renderApp({ at: ESCALATION_PAGE, as: 'joy-bautista' })

    await user.click(await screen.findByRole('radio', { name: 'The patient called me and needs help now' }))
    await user.click(screen.getByRole('button', { name: 'Open the case' }))

    expect(await screen.findByRole('region', { name: 'What acknowledging means' })).toHaveTextContent(
      'Only the facility lead at General Hospital can acknowledge this escalation.',
    )
    expect(within(acknowledgementCard()).queryByRole('button', { name: 'Acknowledge' })).not.toBeInTheDocument()
    expect(sentRequests(CASE_ROUTE).at(-1)?.query.get('reason')).toBe('The patient called me and needs help now')
  })

  it('asks for a confirmation that says acknowledging changes neither who is responsible nor the clock', async () => {
    const { user } = await openEscalation(ESCALATED, 'joy-bautista')
    await user.click(within(acknowledgementCard()).getByRole('button', { name: 'Acknowledge' }))

    const dialog = screen.getByRole('dialog', { name: 'Acknowledge this escalation?' })
    expect(dialog).toHaveTextContent(
      'This records that you have seen it. It does not change who is responsible or stop the clock: Dr. Rekha Varghese stays responsible, and the deadline stays the same.',
    )
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(sentRequests(ACK_ROUTE)).toHaveLength(0)
  })

  it('acknowledges exactly the escalation on screen, then confirms it and shows it acknowledged', async () => {
    answerReads(ESCALATED)
    answer(
      ACK_ROUTE,
      json({
        ...ESCALATED,
        escalations: [
          { ...TO_LEAD, acknowledgedBy: 'joy-bautista', acknowledgedByRole: 'facility-lead', acknowledgedAt: TEST_NOW.toISOString() },
        ],
      }),
    )
    const { user } = renderApp({ at: ESCALATION_PAGE, as: 'joy-bautista' })
    await user.click(within(await screen.findByRole('region', { name: 'What acknowledging means' })).getByRole('button', { name: 'Acknowledge' }))
    await user.click(within(screen.getByRole('dialog', { name: 'Acknowledge this escalation?' })).getByRole('button', { name: 'Acknowledge' }))

    expect(await screen.findByText('Escalation acknowledged.')).toBeInTheDocument()
    expect(within(acknowledgementCard()).getByText(/^Acknowledged by you,/)).toBeInTheDocument()
    expect(within(acknowledgementCard()).queryByRole('button', { name: 'Acknowledge' })).not.toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(sentRequests(ACK_ROUTE).map((request) => request.body)).toEqual([''])
  })

  it.each([
    [409, 'This escalation was already acknowledged, or a newer one replaced it. Close this message to see the latest.'],
    [403, 'Only the facility lead at Family Health Centre can acknowledge this escalation. Close this message to see the latest.'],
  ])('reads a %i calmly, and closing it clears the message and reads the case again (D3)', async (status, message) => {
    const { user } = await openEscalation(ESCALATED, 'joy-bautista')
    answer(ACK_ROUTE, problem(status, 'The acknowledgement was refused.'))
    await user.click(within(acknowledgementCard()).getByRole('button', { name: 'Acknowledge' }))
    const dialog = screen.getByRole('dialog', { name: 'Acknowledge this escalation?' })
    await user.click(within(dialog).getByRole('button', { name: 'Acknowledge' }))

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(message)
    expect(within(dialog).queryByRole('button', { name: 'Acknowledge' })).not.toBeInTheDocument()

    answer(CASE_ROUTE, json(WITH_COORDINATOR))
    const reads = sentRequests(CASE_ROUTE).length
    await user.click(within(dialog).getByRole('button', { name: 'Close' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(await screen.findByText('Only the programme coordinator can acknowledge this escalation.')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(sentRequests(CASE_ROUTE)).toHaveLength(reads + 1)
  })

  it('shows the programme coordinator no name, MRN or hospital record number on the escalation', async () => {
    answerReads({ ...WITH_COORDINATOR, outcomeEvidenceReference: 'Procedure/proc-100245891' })
    answer(
      `GET /cases/${CASE_ID}/timeline`,
      json([
        timelineEntry({
          sourceSystem: 'fhc-his',
          eventType: 'ScreeningResultReceived',
          disposition: 'Rejected',
          actorReference: 'fhc-his',
          actorRole: 'system',
          reason: "A case already exists for result 'fhc-his/DiagnosticReport/r-100245891'; one result opens one case.",
        }),
      ]),
    )
    renderApp({ at: ESCALATION_PAGE, as: 'obaid-al-suwaidi' })

    expect(await screen.findByText('Case 3F6B1C2A · Name hidden for your role · Breast screening')).toBeInTheDocument()
    await screen.findByRole('region', { name: 'What acknowledging means' })
    const page = document.body.textContent ?? ''
    for (const identifier of ['Hessa', '100245891', 'p-100245891', 'DiagnosticReport', 'Procedure/']) {
      expect(page).not.toContain(identifier)
    }
    expect(document.querySelector('[title*="100245891"]')).toBeNull()
  })

  it('still shows the escalation when the programme cannot be read, and says the ladder is incomplete (D5)', async () => {
    answerReads(ESCALATED)
    answer('GET /programmes/breast-screening', problem(500, 'The programme could not be read.'))
    renderApp({ at: ESCALATION_PAGE, as: 'joy-bautista' })

    expect(
      await screen.findByText(
        "The programme's escalation steps aren't available right now, so this shows only the escalations raised so far.",
      ),
    ).toBeInTheDocument()
    expect(within(screen.getByRole('list', { name: 'When a step is late' })).getAllByRole('listitem')).toHaveLength(2)
    expect(within(acknowledgementCard()).getByRole('button', { name: 'Acknowledge' })).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('shows the whole ladder, with no note, once the programme is read', async () => {
    await openEscalation(ESCALATED, 'joy-bautista')
    await waitFor(() =>
      expect(within(screen.getByRole('list', { name: 'When a step is late' })).getAllByRole('listitem')).toHaveLength(3),
    )
    expect(screen.queryByText(/escalation steps aren't available/)).not.toBeInTheDocument()
  })
})
