import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { CaseDetail } from '../../data'
import { answer, json, problem, sentRequests, type SentRequest } from '../../testing/api'
import { caseDetail, caseSummary, escalation, programme, timelineEntry } from '../../testing/builders'
import { renderApp } from '../../testing/screens'

const CASE = caseDetail()
const CASE_PAGE = `#/cases/${CASE.caseId}`
const CASE_ROUTE = `GET /cases/${CASE.caseId}`
const EARLIER = caseSummary({
  caseId: '9f8e7d6c-5b4a-4321-8fed-100245891999',
  createdAt: '2024-11-05T06:00:00Z',
  stage: 'Closed',
  displayLabel: 'Closed',
  closureReason: 'NegativeOnConfirmation',
})

function otherScreening(request: SentRequest): Response {
  return json(request.query.get('patientReference') === CASE.patientReference ? [caseSummary(), EARLIER] : [])
}

function answerReads(detail: CaseDetail): void {
  answer(`GET /cases/${detail.caseId}`, json(detail))
  answer(`GET /cases/${detail.caseId}/timeline`, json([timelineEntry()]))
  answer('GET /programmes/breast-screening', json(programme()))
  answer('GET /cases', otherScreening)
}

function searches(): SentRequest[] {
  return sentRequests('GET /cases').filter((request) => request.query.has('patientReference'))
}

const AT_CLOSURE_REVIEW = caseDetail({
  stage: 'ClosureReview',
  displayLabel: 'ClosureReview',
  nextAction: 'decide-closure',
  outcomeEvidenceKind: 'diagnosis',
  outcomeEvidenceReference: 'Procedure/proc-100245891',
  isEscalated: true,
  exceptions: ['Overdue'],
  escalations: [
    escalation(),
    escalation({
      escalationId: '5d1e2f3a-4b5c-4d6e-8f70-100245891e02',
      level: 2,
      escalatedTo: { reference: 'coordinator:family-health-centre', role: 'coordinator', facilityId: 'family-health-centre', isQueue: true },
      raisedAt: '2026-11-12T04:00:00Z',
    }),
  ],
})
const REPEATED_RESULT = timelineEntry({
  auditEventId: 'a0000000-0000-4000-8000-000000000002',
  sourceSystem: 'fhc-his',
  eventType: 'ScreeningResultReceived',
  disposition: 'Rejected',
  actorReference: 'fhc-his',
  actorRole: 'system',
  sourceTimestamp: '2026-11-11T08:00:00Z',
  receivedTimestamp: '2026-11-11T08:00:00Z',
  reason: "A case already exists for result 'fhc-his/DiagnosticReport/r-100245891'; one result opens one case.",
})
const OUTCOME = timelineEntry({
  auditEventId: 'a0000000-0000-4000-8000-000000000003',
  sourceSystem: 'fhc-his',
  eventType: 'OutcomeRecorded',
  actorReference: 'rekha-varghese',
  actorRole: 'clinician',
  sourceTimestamp: '2026-11-11T09:00:00Z',
  receivedTimestamp: '2026-11-11T09:00:00Z',
  reason: 'OutcomeRecorded applied',
  evidenceReference: 'Procedure/proc-100245891',
})

function answerHospitalTrail(): void {
  answerReads(AT_CLOSURE_REVIEW)
  answer(`GET /cases/${CASE.caseId}/timeline`, json([timelineEntry(), REPEATED_RESULT, OUTCOME]))
}

describe('case', () => {
  it('shows the programme coordinator a case number, never the patient’s name, MRN or hospital record numbers', async () => {
    answerHospitalTrail()
    renderApp({ at: CASE_PAGE, as: 'obaid-al-suwaidi' })

    expect(await screen.findByRole('heading', { level: 1, name: 'Case 3F6B1C2A' })).toBeInTheDocument()
    expect(
      screen.getByText('Names, MRNs and hospital record numbers are hidden for your role. Everything else on the case is here.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Result from the hospital system')).toBeInTheDocument()
    expect(screen.getByText('Outcome evidence is recorded in the hospital system.')).toBeInTheDocument()
    expect(await screen.findByText('Result not accepted')).toBeInTheDocument()
    expect(screen.getByText('Deadline warning raised')).toBeInTheDocument()
    const page = document.body.textContent ?? ''
    for (const identifier of [
      'Hessa',
      'Al Dhaheri',
      '100245891',
      '100\u00a0245\u00a0891',
      'p-100245891',
      'DiagnosticReport',
      'Procedure/',
    ]) {
      expect(page).not.toContain(identifier)
    }
    expect(page).not.toMatch(/MRN\s\d/)
    expect(document.querySelector('[title*="100245891"]')).toBeNull()
    expect(document.title).toBe('Case 3F6B1C2A · CareLoop')
    expect(searches()).toHaveLength(0)
  })

  it('still shows staff the hospital’s result number, outcome evidence and the reason a result was refused', async () => {
    answerHospitalTrail()
    renderApp({ at: CASE_PAGE, as: 'rekha-varghese' })

    expect(await screen.findByText('Result DiagnosticReport/r-100245891 from the hospital system')).toBeInTheDocument()
    expect(screen.getByText('Outcome evidence: Procedure/proc-100245891')).toBeInTheDocument()
    expect(
      await screen.findByText("A case already exists for result 'fhc-his/DiagnosticReport/r-100245891'; one result opens one case."),
    ).toBeInTheDocument()
  })

  it.each([
    ['the nurse navigator', 'huda-saleh', 'family-health-centre'],
    ['a clinician', 'rekha-varghese', 'family-health-centre'],
    ['the facility lead', 'joy-bautista', 'family-health-centre'],
    ['a receiving clinician', 'layla-haddad', 'general-hospital'],
  ])('shows %s the patient’s name and MRN', async (_who, as, facilityId) => {
    answerReads(caseDetail({ facilityId }))
    renderApp({ at: CASE_PAGE, as })

    expect(await screen.findByRole('heading', { level: 1, name: 'Hessa Al Dhaheri' })).toBeInTheDocument()
    expect(screen.getByText('MRN 100 245 891')).toBeInTheDocument()
    expect(screen.queryByText(/hidden for your role/)).not.toBeInTheDocument()
  })

  it.each([
    ['a clinician', 'rekha-varghese', 'family-health-centre', true],
    ['a receiving clinician', 'layla-haddad', 'general-hospital', true],
    ['the nurse navigator', 'huda-saleh', 'family-health-centre', false],
    ['the facility lead', 'joy-bautista', 'family-health-centre', false],
  ])('shows the patient’s other screening to %s only if they are a clinician', async (_who, as, facilityId, shown) => {
    answerReads(caseDetail({ facilityId }))
    renderApp({ at: CASE_PAGE, as })
    await screen.findByRole('heading', { level: 1, name: 'Hessa Al Dhaheri' })
    await screen.findByText('Deadline warning raised')

    const card = screen.queryByRole('region', { name: "Hessa Al Dhaheri's other screening" })
    if (shown) {
      expect(card).toBeInTheDocument()
      expect(await within(card as HTMLElement).findByRole('link', { name: 'Breast screening, opened Tue 5 Nov 2024' })).toHaveAttribute(
        'href',
        `#/cases/${EARLIER.caseId}`,
      )
      expect(searches().map((request) => request.query.get('patientReference'))).toEqual([CASE.patientReference])
    } else {
      expect(card).not.toBeInTheDocument()
      expect(searches()).toHaveLength(0)
    }
  })

  it('tells the programme coordinator calmly that a case not escalated to them is not open to them', async () => {
    answer(CASE_ROUTE, problem(403, 'This case is not escalated to the coordinator.'))
    answer(`GET /cases/${CASE.caseId}/timeline`, problem(403, 'This case is not escalated to the coordinator.'))
    answer('GET /cases', json([]))
    renderApp({ at: CASE_PAGE, as: 'obaid-al-suwaidi' })

    const title = await screen.findByText('This case is not open to you')
    expect(title.closest('[role="status"]')).toHaveTextContent(
      'A case opens for the programme coordinator only while it is escalated to them.',
    )
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument()
  })

  it('asks staff why first, then says calmly that a case still refused is not open to them', async () => {
    answer(CASE_ROUTE, problem(403, 'Outside your facility.'))
    answer(`GET /cases/${CASE.caseId}/timeline`, problem(403, 'Outside your facility.'))
    const { user } = renderApp({ at: CASE_PAGE, as: 'huda-saleh' })

    await user.click(await screen.findByRole('radio', { name: "I am covering for the patient's nurse today" }))
    await user.click(screen.getByRole('button', { name: 'Open the case' }))

    const title = await screen.findByText('This case is not open to you')
    expect(title.closest('[role="status"]')).toHaveTextContent(
      "Your role or team can't open this case. If you need it, ask the person responsible for it.",
    )
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(sentRequests(CASE_ROUTE).at(-1)?.query.get('reason')).toBe("I am covering for the patient's nurse today")
  })

  it('still opens the case when its programme cannot be read, and says the ladder is incomplete (D5)', async () => {
    answerReads(caseDetail({ isEscalated: true, exceptions: ['Overdue'], escalations: [escalation()] }))
    answer('GET /programmes/breast-screening', problem(500, 'The programme could not be read.'))
    renderApp({ at: CASE_PAGE, as: 'rekha-varghese' })

    expect(await screen.findByRole('heading', { level: 1, name: 'Hessa Al Dhaheri' })).toBeInTheDocument()
    expect(
      await screen.findByText(
        "The programme's escalation steps aren't available right now, so this shows only the escalations raised so far.",
      ),
    ).toBeInTheDocument()
    expect(within(screen.getByRole('list', { name: 'When a step is late' })).getAllByRole('listitem')).toHaveLength(2)
    expect(screen.getByRole('heading', { level: 2, name: 'Arrange the follow-up' })).toBeInTheDocument()
    expect(await screen.findByText('Deadline warning raised')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
