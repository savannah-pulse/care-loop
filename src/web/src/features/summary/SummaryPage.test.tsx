import { screen, within } from '@testing-library/react'
import type { UserEvent } from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { answer, json, problem, sentRequests, type SentRequest } from '../../testing/api'
import { TEST_NOW, caseDetail, handoffSummary } from '../../testing/builders'
import { renderApp, type RenderedApp } from '../../testing/screens'

const CASE = caseDetail()
const DRAFT = handoffSummary()
const SUMMARY_PAGE = `#/cases/${CASE.caseId}/summary`
const DRAFT_ROUTE = `POST /cases/${CASE.caseId}/handoff-summary`
const APPROVAL_ROUTE = `POST /cases/${CASE.caseId}/handoff-summary/${DRAFT.summaryId}/approval`
const REJECTION_ROUTE = `POST /cases/${CASE.caseId}/handoff-summary/${DRAFT.summaryId}/rejection`

function answerCaseReads(): void {
  answer(`GET /cases/${CASE.caseId}`, json(CASE))
  answer(`GET /cases/${CASE.caseId}/timeline`, json([]))
  answer('GET /cases', json([]))
}

function approved(request: SentRequest): Response {
  const { approvedText } = JSON.parse(request.body) as { approvedText: string }
  return json({
    ...DRAFT,
    status: 'Approved',
    text: approvedText,
    humanReviewRequired: false,
    approvedBy: 'rekha-varghese',
    approvedAt: TEST_NOW.toISOString(),
  })
}

function rejected(request: SentRequest): Response {
  const { reason } = JSON.parse(request.body) as { reason: string }
  return json({
    ...DRAFT,
    status: 'Rejected',
    rejectedBy: 'rekha-varghese',
    rejectedAt: TEST_NOW.toISOString(),
    rejectionReason: reason,
  })
}

async function openDraft(): Promise<RenderedApp> {
  answerCaseReads()
  answer(DRAFT_ROUTE, json(DRAFT, 201))
  answer(APPROVAL_ROUTE, approved)
  answer(REJECTION_ROUTE, rejected)
  const app = renderApp({ at: SUMMARY_PAGE, as: 'rekha-varghese' })
  await app.user.click(await screen.findByRole('button', { name: 'Draft the summary' }))
  await screen.findByRole('group', { name: 'Decide on this summary' })
  return app
}

function actionBar(): HTMLElement {
  return screen.getByRole('group', { name: 'Decide on this summary' })
}

async function edit(user: UserEvent, typed: Readonly<Record<string, string>>): Promise<void> {
  await user.click(within(actionBar()).getByRole('button', { name: 'Edit' }))
  for (const [heading, text] of Object.entries(typed)) {
    const field = screen.getByRole('textbox', { name: heading })
    await user.clear(field)
    await user.type(field, text)
  }
}

async function approve(user: UserEvent): Promise<void> {
  await user.click(within(actionBar()).getByRole('button', { name: 'Approve summary' }))
  const dialog = screen.getByRole('dialog', { name: 'Approve this summary?' })
  await user.click(within(dialog).getByRole('button', { name: 'Approve summary' }))
}

async function reject(user: UserEvent, reason: string): Promise<void> {
  await user.click(within(actionBar()).getByRole('button', { name: 'Reject' }))
  const dialog = screen.getByRole('dialog', { name: 'Reject this draft?' })
  await user.type(within(dialog).getByRole('textbox', { name: 'Why are you rejecting it?' }), reason)
  await user.click(within(dialog).getByRole('button', { name: 'Reject draft' }))
}

async function leaveAndComeBack(user: UserEvent): Promise<void> {
  await user.click(screen.getByRole('link', { name: 'Hessa Al Dhaheri' }))
  await user.click(await screen.findByRole('link', { name: 'Review the handoff summary' }))
}

async function switchPerson(user: UserEvent, name: string): Promise<void> {
  await user.click(screen.getByRole('button', { name: (label) => label.startsWith('Signed in as') }))
  await user.click(screen.getByRole('button', { name: (label) => label.startsWith(name) }))
}

describe('handoff summary', () => {
  it('approves exactly the edited text, untrimmed and byte for byte', async () => {
    const { user } = await openDraft()
    await edit(user, {
      Situation: '  Hessa’s son has the clinic letter.{Enter}{Enter}Call before 10\u00a0am.',
      Recommendation: 'Book the assessment clinic.{Enter}  ',
    })
    await approve(user)

    expect(await screen.findByText('Summary approved.')).toBeInTheDocument()
    const expected = [
      'Situation\n  Hessa’s son has the clinic letter.\n\nCall before 10\u00a0am.',
      'Assessment\nFor the clinician to write.',
      'Recommendation\nBook the assessment clinic.\n  ',
    ].join('\n\n')
    const approvals = sentRequests(APPROVAL_ROUTE)
    expect(approvals).toHaveLength(1)
    expect(approvals[0].body).toBe(JSON.stringify({ approvedText: expected }))
    expect(screen.getByText(/^Approved by you,/)).toBeInTheDocument()
  })

  it('approves only after the confirmation step, and cancelling sends nothing', async () => {
    const { user } = await openDraft()
    await user.click(within(actionBar()).getByRole('button', { name: 'Approve summary' }))
    const dialog = screen.getByRole('dialog', { name: 'Approve this summary?' })
    expect(dialog).toHaveTextContent('It goes with the referral when you send it from your hospital system.')
    expect(sentRequests(APPROVAL_ROUTE)).toHaveLength(0)

    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(sentRequests(APPROVAL_ROUTE)).toHaveLength(0)

    await approve(user)
    expect(await screen.findByText(/^Approved by you,/)).toBeInTheDocument()
    expect(sentRequests(APPROVAL_ROUTE).map((request) => request.body)).toEqual([
      JSON.stringify({ approvedText: DRAFT.text }),
    ])
  })

  it('keeps the draft and the edits when the reviewer leaves the summary and comes back (D1)', async () => {
    const { user } = await openDraft()
    await edit(user, { Situation: 'Needs assessment.{Enter}Her son has the clinic letter.  ' })

    await leaveAndComeBack(user)

    expect(await screen.findByText('Edited by you')).toBeInTheDocument()
    await user.click(within(actionBar()).getByRole('button', { name: 'Edit' }))
    expect(screen.getByRole('textbox', { name: 'Situation' })).toHaveValue(
      'Needs assessment.\nHer son has the clinic letter.  ',
    )
    expect(sentRequests(DRAFT_ROUTE)).toHaveLength(1)

    await approve(user)
    expect(await screen.findByText('Summary approved.')).toBeInTheDocument()
    expect(JSON.parse(sentRequests(APPROVAL_ROUTE)[0].body)).toEqual({
      approvedText: [
        'Situation\nNeeds assessment.\nHer son has the clinic letter.  ',
        'Assessment\nFor the clinician to write.',
        'Recommendation\nBook the assessment clinic at the General Hospital.',
      ].join('\n\n'),
    })
  })

  it.each([
    ['approved', (user: UserEvent) => approve(user), 'Summary approved.'],
    ['rejected', (user: UserEvent) => reject(user, 'The history is out of date.'), 'Draft rejected. Your reason is kept with it.'],
  ])('forgets the draft once it is %s', async (_decision, decide, confirmation) => {
    const { user } = await openDraft()
    await edit(user, { Situation: 'Checked with the patient.' })
    await decide(user)
    expect(await screen.findByText(confirmation)).toBeInTheDocument()

    await leaveAndComeBack(user)

    expect(await screen.findByRole('button', { name: 'Draft the summary' })).toBeInTheDocument()
    expect(screen.queryByText('Checked with the patient.')).not.toBeInTheDocument()
  })

  it('forgets the draft when someone else signs in', async () => {
    const { user } = await openDraft()
    await edit(user, { Situation: 'Checked with the patient.' })

    await switchPerson(user, 'Dr. Sara Al Hammadi')
    expect(await screen.findByRole('button', { name: 'Draft the summary' })).toBeInTheDocument()

    await switchPerson(user, 'Dr. Rekha Varghese')
    expect(await screen.findByRole('button', { name: 'Draft the summary' })).toBeInTheDocument()
    expect(screen.queryByText('Edited by you')).not.toBeInTheDocument()
    expect(sentRequests(DRAFT_ROUTE)).toHaveLength(1)
  })

  it('forgets a draft the API says was decided or replaced, so coming back offers a new one', async () => {
    const { user } = await openDraft()
    answer(APPROVAL_ROUTE, problem(409, 'This summary has already been decided.'))
    await approve(user)
    const dialog = screen.getByRole('dialog', { name: 'Approve this summary?' })
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'This draft was already decided or replaced. Draft the summary again.',
    )
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    await leaveAndComeBack(user)

    expect(await screen.findByRole('button', { name: 'Draft the summary' })).toBeInTheDocument()
  })

  it('rejects only with a reason, sent without the spaces around it', async () => {
    const { user } = await openDraft()
    await user.click(within(actionBar()).getByRole('button', { name: 'Reject' }))
    const dialog = screen.getByRole('dialog', { name: 'Reject this draft?' })
    const reason = within(dialog).getByRole('textbox', { name: 'Why are you rejecting it?' })
    const confirm = within(dialog).getByRole('button', { name: 'Reject draft' })

    await user.click(confirm)
    expect(dialog).toHaveTextContent('Give a reason before rejecting.')
    expect(reason).toHaveAttribute('aria-invalid', 'true')
    expect(reason).toHaveFocus()

    await user.type(reason, '   ')
    await user.click(confirm)
    expect(dialog).toHaveTextContent('Give a reason before rejecting.')
    expect(sentRequests(REJECTION_ROUTE)).toHaveLength(0)

    await user.type(reason, 'The history is out of date.  ')
    await user.click(confirm)
    expect(await screen.findByText('Draft rejected. Your reason is kept with it.')).toBeInTheDocument()
    expect(sentRequests(REJECTION_ROUTE).map((request) => request.body)).toEqual([
      JSON.stringify({ reason: 'The history is out of date.' }),
    ])
    expect(screen.getByText(/^Rejected by you,/)).toBeInTheDocument()
    expect(screen.getByText('Reason: The history is out of date.')).toBeInTheDocument()
  })

  it('shows the programme coordinator where the result came from, without the hospital’s number', async () => {
    answerCaseReads()
    renderApp({ at: SUMMARY_PAGE, as: 'obaid-al-suwaidi' })

    expect(await screen.findByText('Result from the hospital system')).toBeInTheDocument()
    expect(screen.getByText('For clinicians')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Case 3F6B1C2A' })).toBeInTheDocument()
    const page = document.body.textContent ?? ''
    for (const identifier of ['Hessa', '100245891', 'DiagnosticReport']) {
      expect(page).not.toContain(identifier)
    }
    expect(document.querySelector('[title*="100245891"]')).toBeNull()
  })

  it('says calmly that drafting is not ready yet when the API answers 501', async () => {
    answerCaseReads()
    answer(DRAFT_ROUTE, problem(501, 'Handoff summaries are not available yet.'))
    const { user } = renderApp({ at: SUMMARY_PAGE, as: 'rekha-varghese' })
    await user.click(await screen.findByRole('button', { name: 'Draft the summary' }))

    expect(await screen.findByRole('heading', { name: 'No AI draft' })).toBeInTheDocument()
    expect(screen.getByText("Drafting isn't ready yet.").closest('[role="status"]')).not.toBeNull()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()

    answer(DRAFT_ROUTE, json(DRAFT, 201))
    await user.click(screen.getByRole('button', { name: 'Check again' }))
    expect(await screen.findByRole('group', { name: 'Decide on this summary' })).toBeInTheDocument()
    expect(sentRequests(DRAFT_ROUTE)).toHaveLength(2)
  })
})
